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
    public class RemoteStorageMonitor : IncomingServerNotifications, ISyncService, IDisposable
    {
        /// <summary>
        /// Virtual drive.
        /// </summary>
        private new readonly VirtualEngine Engine;

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
        /// Creates instance of this class.
        /// </summary>
        /// <param name="webSocketServerUrl">WebSocket server url.</param>
        /// <param name="engine">Engine to send notifications about changes in the remote storage.</param>
        internal RemoteStorageMonitor(string webSocketServerUrl, VirtualEngine engine)
            : base(engine, engine.Logger.CreateLogger("Remote Storage Monitor"))
        {
            this.Engine = engine;
            this.webSocketServerUrl = webSocketServerUrl;
        }

        /// <summary>
        /// Starts monitoring changes in the remote storage.
        /// </summary>
        private async Task StartMonitoringAsync(NetworkCredential credentials, CookieCollection cookies, Guid thisInstanceId)
        {
            cancellationTokenSource = new CancellationTokenSource();
            clientWebSocket = new ClientWebSocket();
            clientWebSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
            clientWebSocket.Options.Credentials = credentials;
            clientWebSocket.Options.Cookies = new CookieContainer();
            clientWebSocket.Options.Cookies.Add(cookies);
            clientWebSocket.Options.SetRequestHeader("InstanceId", thisInstanceId.ToString());

            await clientWebSocket.ConnectAsync(new Uri(webSocketServerUrl), CancellationToken.None);

            Logger.LogMessage("Started", webSocketServerUrl);

            var rcvBuffer = new ArraySegment<byte>(new byte[2048]);
            while (true)
            {
                WebSocketReceiveResult rcvResult = await clientWebSocket.ReceiveAsync(rcvBuffer, cancellationTokenSource.Token);
                byte[] msgBytes = rcvBuffer.Skip(rcvBuffer.Offset).Take(rcvResult.Count).ToArray();
                string rcvMsg = Encoding.UTF8.GetString(msgBytes);
                await ProcessAsync(rcvMsg);
            }
        }

        /// <summary>
        /// Starts websockets to monitor changes in remote storage.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await Task.Factory.StartNew(
              async () =>
              {
                  bool repeat = false;
                  do
                  {
                      try
                      {
                          repeat = false;
                          await StartMonitoringAsync(Engine.Credentials, Engine.Cookies, Engine.InstanceId);
                      }
                      catch (Exception e) when (e is WebSocketException || e is AggregateException)
                      {
                          // Start socket after first success WebDAV PROPFIND. Restart socket when it disconnects.
                          if (clientWebSocket != null && clientWebSocket?.State != WebSocketState.Closed)
                          {
                              Logger.LogError(e.Message, webSocketServerUrl);
                          }

                          // Delay websocket connect to not overload it on network disappear.
                          await Task.Delay(TimeSpan.FromSeconds(2));
                          repeat = true;
                      };
                  } while (repeat);
              }, new CancellationTokenSource().Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
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
        /// Processes notification received from server via WebSockets. Triggers reading all changes from the server. 
        /// </summary>
        internal async Task ProcessAsync(string jsonString)
        {
            WebSocketMessage jsonMessage = JsonSerializer.Deserialize<WebSocketMessage>(jsonString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            string remoteStoragePath = Mapping.GetAbsoluteUri(jsonMessage.ItemPath);

            // Check if remote URL starts with WebDAVServerUrl.
            if (remoteStoragePath.StartsWith(Program.Settings.WebDAVServerUrl, StringComparison.InvariantCultureIgnoreCase))
            {
                Logger.LogDebug($"EventType: {jsonMessage.EventType}", jsonMessage.ItemPath, jsonMessage.TargetPath);
                try
                {
                    // triggers ISynchronizationCollection.GetChangesAsync call to get all changes from server.                    
                    await (Engine.ServerNotifications(Engine.Path, Logger) as IServerCollectionNotifications)
                        .ProcessChangesAsync(async (metadata, userFileSystemPath) => 
                        await Engine.Placeholders.GetItem(userFileSystemPath).SavePropertiesAsync(metadata as FileSystemItemMetadataExt, Logger));

                }
                catch(Exception ex)
                {
                    Logger.LogError(nameof(ProcessAsync), Engine.Path, null, ex);
                }
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
    internal class WebSocketMessage
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
