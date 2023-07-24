using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;


namespace WebDAVDrive
{
    /// <summary>
    /// Monitors changes in the remote storage, notifies the client and updates the user file system.
    /// If any file or folder is created, updated, delated, moved, locked or unlocked in the remote storage, 
    /// triggers an event with information about changes being made.
    /// </summary>
    public class RemoteStorageMonitor: ISyncService, IDisposable
    {
        /// <summary>
        /// Credentials to authenticate web sockets.
        /// </summary>
        public NetworkCredential Credentials;

        /// <summary>
        /// Cookies to add to web sockets requests.
        /// </summary>
        public CookieCollection Cookies;

        /// <summary>
        /// Engine instance ID, to avoid sending notifications back to originating client. 
        /// </summary>
        public Guid InstanceId = Guid.Empty;

        /// <summary>
        /// Server notifications will be sent to this object.
        /// </summary>
        public IServerCollectionNotifications ServerNotifications;

        /// <summary>
        /// Called to save item properties.
        /// </summary>
        public SavePropertiesAction SavePropertiesAction = null;

        /// <summary>
        /// Logger.
        /// </summary>
        public readonly ILogger Logger;

        /// <summary>
        /// Current synchronization state.
        /// </summary>
        public virtual SynchronizationState SyncState
        {
            get
            {
                return
                (clientWebSocket != null
                    && (clientWebSocket?.State == WebSocketState.Open || clientWebSocket?.State == WebSocketState.CloseSent || clientWebSocket?.State == WebSocketState.CloseReceived))
                    ? SynchronizationState.Enabled : SynchronizationState.Disabled;
            }
        }

        /// <summary>
        /// WebSocket client.
        /// </summary>
        private ClientWebSocket clientWebSocket;

        /// <summary>
        /// WebSocket server url.
        /// </summary>
        private readonly string webSocketServerUrl;

        /// <summary>
        /// WebSocket cancellation token.
        /// </summary>
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// Changes are being processed.
        /// </summary>
        private bool processing = false;

        /// <summary>
        /// More changes arrived while chenges are still processing.
        /// </summary>
        private bool moreChangesToProcess = false;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="webSocketServerUrl">WebSocket server url.</param>
        /// <param name="logger">Logger.</param>
        internal RemoteStorageMonitor(string webSocketServerUrl, ILogger logger)
        {
            this.Logger = logger.CreateLogger("Remote Storage Monitor");
            this.webSocketServerUrl = webSocketServerUrl;
        }

        /// <summary>
        /// Monitors and processes WebSockets notifications from the remote storage.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <remarks>
        /// Listens to server notifications, gets all changes from the remote storage on every notification.
        /// </remarks>
        private async Task RunWebSocketsAsync(CancellationToken cancellationToken)
        {
            var rcvBuffer = new ArraySegment<byte>(new byte[2048]);
            while (!cancellationToken.IsCancellationRequested)
            {
                WebSocketReceiveResult rcvResult = await clientWebSocket.ReceiveAsync(rcvBuffer, cancellationToken);
                byte[] msgBytes = rcvBuffer.Skip(rcvBuffer.Offset).Take(rcvResult.Count).ToArray();
                string rcvMsg = Encoding.UTF8.GetString(msgBytes);

                WebSocketMessage webSocketMessage = JsonSerializer.Deserialize<WebSocketMessage>(rcvMsg, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Because of the on-demand loading, item or its parent may not exists or be offline.
                // We can ignore notifiction in this case and avoid many requests to the remote storage.
                if (!Filter(webSocketMessage))
                {
                    await ProcessAsync();
                }
            }
        }

        /// <summary>
        /// Verifies that the WebSockets message is for the item that exists 
        /// in the user file system and should be updated.
        /// </summary>
        /// <param name="webSocketMessage">Information about change in the remote storage.</param>
        /// <returns>True if the item exists and should be updated. False otherwise.</returns>
        protected virtual bool Filter(WebSocketMessage webSocketMessage)
        {
            string remoteStoragePath = Mapping.GetAbsoluteUri(webSocketMessage.ItemPath);

            // Just in case there is more than one WebSockets server/virtual folder that
            // is sending notifications (like with webdavserver.net, webdavserver.com),
            // here we filter notifications that come from a different server/virtual folder.
            if (remoteStoragePath.StartsWith(Program.Settings.WebDAVServerUrl, StringComparison.InvariantCultureIgnoreCase))
            {
                Logger.LogDebug($"EventType: {webSocketMessage.EventType}", webSocketMessage.ItemPath, webSocketMessage.TargetPath);

                string userFileSystemPath = Mapping.ReverseMapPath(remoteStoragePath);
                switch (webSocketMessage.EventType)
                {
                    case "created":
                    case "deleted":
                        // Verify that parent folder exists and is not offline.
                        string userFileSystemParentPath = Path.GetDirectoryName(userFileSystemPath);
                        return !Directory.Exists(userFileSystemParentPath)
                            || new DirectoryInfo(userFileSystemParentPath).Attributes.HasFlag(FileAttributes.Offline);

                    case "moved":
                        // Verify that source exists OR target folder exists and is not offline.
                        if (File.Exists(userFileSystemPath))
                        {
                            return false;
                        }
                        else
                        {
                            string remoteStorageNewPath = Mapping.GetAbsoluteUri(webSocketMessage.TargetPath);
                            string userFileSystemNewPath = Mapping.ReverseMapPath(remoteStorageNewPath);
                            string userFileSystemNewParentPath = Path.GetDirectoryName(userFileSystemNewPath);
                            return !Directory.Exists(userFileSystemNewParentPath)
                                || new DirectoryInfo(userFileSystemNewParentPath).Attributes.HasFlag(FileAttributes.Offline);
                        }

                    case "updated":
                    default:
                        // Any other notifications.
                        return !File.Exists(userFileSystemPath);
                }
            }

            return true;
        }

        /// <summary>
        /// Starts monitoring changes in the remote storage.
        /// </summary>
        /// <param name="cookies">Cookies to add to web sockets requests.</param>

        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Starting", webSocketServerUrl);

            await Task.Factory.StartNew(
              async () =>
              {
                  using (cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                  {
                      bool repeat = false;
                      do
                      {
                          using (clientWebSocket = new ClientWebSocket())
                          {
                              try
                              {
                                  repeat = false;

                                  // Configure web sockets and connect to the server.
                                  clientWebSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
                                  if (Credentials != null)
                                  {
                                      clientWebSocket.Options.Credentials = Credentials;
                                  }
                                  if (Cookies != null)
                                  {
                                      clientWebSocket.Options.Cookies = new CookieContainer();
                                      clientWebSocket.Options.Cookies.Add(Cookies);
                                  }
                                  if (InstanceId != Guid.Empty)
                                  { 
                                      clientWebSocket.Options.SetRequestHeader("InstanceId", InstanceId.ToString());
                                  }
                                  await clientWebSocket.ConnectAsync(new Uri(webSocketServerUrl), cancellationToken);
                                  Logger.LogMessage("Connected", webSocketServerUrl);

                                  // After esteblishing connection with a server we must get all changes from the remote storage.
                                  // This is required on Engine start, server recovery, network recovery, etc.
                                  Logger.LogDebug("Getting all changes from server", webSocketServerUrl);
                                  await ProcessAsync();

                                  Logger.LogMessage("Started", webSocketServerUrl);

                                  await RunWebSocketsAsync(cancellationTokenSource.Token);
                              }
                              catch (Exception e) when (e is WebSocketException || e is AggregateException)
                              {
                                  // Start socket after first successeful WebDAV PROPFIND. Restart socket if disconnected.
                                  if (clientWebSocket != null && clientWebSocket?.State != WebSocketState.Closed)
                                  {
                                      Logger.LogError(e.Message, webSocketServerUrl, null, e);
                                  }

                                  // Here we delay WebSocket connection to avoid overload on
                                  // network disconnections or server failure.
                                  await Task.Delay(TimeSpan.FromSeconds(2), cancellationTokenSource.Token);
                                  repeat = true;
                              };
                          }
                      } while (repeat);
                  }
              }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// Stops monitoring changes in the remote storage.
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                cancellationTokenSource?.Cancel();
                if (clientWebSocket != null
                    && (clientWebSocket?.State == WebSocketState.Open || clientWebSocket?.State == WebSocketState.CloseSent || clientWebSocket?.State == WebSocketState.CloseReceived))
                {
                    await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
            }
            catch (WebSocketException ex)
            {
                Logger.LogError("Failed to close websocket.", webSocketServerUrl, null, ex);
            };

            Logger.LogMessage("Stoped", webSocketServerUrl);
        }

        /// <summary>
        /// Triggers <see cref="ISynchronizationCollection.GetChangesAsync"/> call to get 
        /// and process all changes from the remote storage.
        /// </summary>
        /// <remarks>
        /// We do not pass WebSockets cancellation token to this method because stopping 
        /// web sockets should not stop processing changes. 
        /// To stop processing changes that are already received the Engine must be stopped.
        /// </remarks>
        private async Task ProcessAsync()
        {
            // This code reduces number of requests to the remote storage, creating 1 item queue.
            // If ProcessChangesAsync() takes 5 sec to execute and 8 notifications
            // arrive from remote storage during execution only the last one will be processed.
            lock (this)
            {
                if(processing)
                {
                    // We do not want to execute multiple requests concurrently.
                    // All changes will be returned in the last request.
                    moreChangesToProcess = true;
                    return; 
                }

                processing = true;
            }

            try
            {
                await ServerNotifications.ProcessChangesAsync(SavePropertiesAction);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to process changes", null, null, ex);
            }

            processing = false;

            if(moreChangesToProcess)
            {
                // More changes has arrived while ProcessChangesAsync() was executing, read those changes.
                moreChangesToProcess = false;
                await ProcessAsync();
            }
        }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    clientWebSocket?.Dispose();
                    cancellationTokenSource?.Dispose();
                    Logger.LogMessage($"Disposed");
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ServerChangesMonitor()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Represents information about changes on the server.
    /// </summary>
    public class WebSocketMessage
    {
        /// <summary>
        /// Operation type: "created", "updated", "moved", "deleted", "locked", "unlocked".
        /// </summary>
        public string EventType { get; set; }

        /// <summary>
        /// Item path in the remote storage. Source item path in case of the move operation.
        /// </summary>
        public string ItemPath { get; set; }

        /// <summary>
        /// Target item path in the remote storage. Provided only in case of the move operation.
        /// </summary>
        public string TargetPath { get; set; }
    }
}
