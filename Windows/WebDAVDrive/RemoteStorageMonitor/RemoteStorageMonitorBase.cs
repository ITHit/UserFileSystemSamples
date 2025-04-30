using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;


namespace WebDAVDrive
{
    /// <summary>
    /// Monitors changes in the remote storage, notifies the client and updates the user file system.
    /// </summary>
    public abstract class RemoteStorageMonitorBase : ISyncService, IDisposable
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
        /// Logger.
        /// </summary>
        public readonly ILogger Logger;

        /// <summary>
        /// Sync mode that corresponds with this remote storage monitor type;
        /// </summary>
        public virtual ITHit.FileSystem.Synchronization.IncomingSyncMode SyncMode { get; }


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
        /// Maximum number of items allowed in the message queue.
        /// If queue cntains more messages, extra messages will be ignored.
        /// This property is required for Sync ID mode.
        /// </summary>
        private readonly int MaxQueueLength;

        /// <summary>
        /// WebSocket client.
        /// </summary>
        private ClientWebSocket clientWebSocket;

        /// <summary>
        /// WebSocket server url.
        /// </summary>
        protected readonly string WebSocketServerUrl;

        /// <summary>
        /// WebSocket cancellation token.
        /// </summary>
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// Contains task which handles events from websocket.
        /// </summary>
        private Task? handlerChangesTask;

        /// <summary>
        /// Changes queue.
        /// </summary>
        /// <remarks>
        /// This is a one item queue which reduces number of requests to the remote storage. 
        /// If ProcessChangesAsync() takes 5 sec to execute and 8 notifications
        /// arrive from remote storage during execution only the last one will be processed.
        /// We do not want to execute multiple requests concurrently.
        /// </remarks>
        private BlockingCollection<WebSocketMessage> changeQueue = new BlockingCollection<WebSocketMessage>();

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="webSocketServerUrl">WebSocket server url.</param>
        /// <param name="maxQueueLength">Maximum number of items allowed in the message queue.</param>
        /// <param name="logger">Logger.</param>
        internal RemoteStorageMonitorBase(string webSocketServerUrl, int maxQueueLength, ILogger logger)
        {
            WebSocketServerUrl = webSocketServerUrl;
            MaxQueueLength = maxQueueLength;
            Logger = logger;
        }

        /// <summary>
        /// Verifies that the WebSockets message is for the item that exists 
        /// in the user file system and should be updated.
        /// </summary>
        /// <param name="webSocketMessage">Information about change in the remote storage.</param>
        /// <returns>True if the item does NOT exists in user file system and should NOT be updated. False - otherwise.</returns>
        public abstract bool Filter(WebSocketMessage webSocketMessage);

        /// <summary>
        /// Monitors and processes WebSockets notifications from the remote storage.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <remarks>
        /// Listens to server notifications, gets all changes from the remote storage on every notification.
        /// </remarks>
        private async Task RunWebSocketsAsync(CancellationToken cancellationToken)
        {
            ArraySegment<byte> rcvBuffer = new ArraySegment<byte>(new byte[2048]);
            while (!cancellationToken.IsCancellationRequested)
            {
                WebSocketReceiveResult rcvResult = await clientWebSocket.ReceiveAsync(rcvBuffer, cancellationToken);
                byte[] msgBytes = rcvBuffer.Skip(rcvBuffer.Offset).Take(rcvResult.Count).ToArray();
                string rcvMsg = Encoding.UTF8.GetString(msgBytes);

                WebSocketMessage? webSocketMessage = JsonSerializer.Deserialize<WebSocketMessage>(rcvMsg, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Because of the on-demand loading, item or its parent may not exists or be offline.
                // We can ignore notifiction in this case and avoid many requests to the remote storage.
                if (webSocketMessage != null && !Filter(webSocketMessage) && changeQueue.Count <= (MaxQueueLength-1))
                {
                    changeQueue.Add(webSocketMessage);
                }
            }
        }

        /// <summary>
        /// Starts monitoring changes in the remote storage.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            // Start sockets after first successeful WebDAV PROPFIND. 
            Logger.LogDebug("Starting", WebSocketServerUrl);

            // Configure web sockets and connect to the server.
            // If connection fails this method throws exception.
            // This will signal to the caller that web sockets are not supported.
            clientWebSocket = await ConnectWebSocketsAsync(cancellationToken);
            
            await Task.Factory.StartNew(
                async () =>
                {
                    using (cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        // Create task for processing websocket events.
                        handlerChangesTask = CreateHandlerChangesTask(cancellationTokenSource.Token);

                        // Restart socket if disconnected. 
                        bool repeat = false;
                        do
                        {
                            using (clientWebSocket ??= await ConnectWebSocketsAsync(cancellationToken))
                            {
                                try
                                {
                                    repeat = false;

                                    // After esteblishing connection with a server, 
                                    // we must get all changes from the remote storage.
                                    // This is required on Engine start, server recovery, network recovery, etc.
                                    Logger.LogDebug("Getting changes from server", WebSocketServerUrl);
                                    await ProcessAsync(null);

                                    Logger.LogMessage("Started", WebSocketServerUrl);

                                    await RunWebSocketsAsync(cancellationTokenSource.Token);
                                }
                                catch (Exception e) when (e is WebSocketException || e is AggregateException)
                                {                                    
                                    if (clientWebSocket != null && clientWebSocket?.State != WebSocketState.Closed)
                                    {
                                        Logger.LogMessage(e.Message, WebSocketServerUrl, null);
                                    }
                                    else
                                    {
                                        Logger.LogDebug(e.Message, WebSocketServerUrl);
                                    }

                                    // Here we delay WebSocket connection to avoid overload on
                                    // network disconnections or server failure.
                                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationTokenSource.Token);
                                    repeat = true;
                                };
                            }
                            clientWebSocket = null;
                        } while (repeat && !cancellationToken.IsCancellationRequested);
                    }
              }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// Configures web sockets and connects to the server.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        private async Task<ClientWebSocket> ConnectWebSocketsAsync(CancellationToken cancellationToken)
        {
            clientWebSocket = new ClientWebSocket();

            // Configure web sockets.
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

            // Connect to the server.
            await clientWebSocket.ConnectAsync(new Uri(WebSocketServerUrl), cancellationToken);
            Logger.LogMessage("Connected", WebSocketServerUrl);

            return clientWebSocket;
        }

        /// <summary>
        /// Stops monitoring changes in the remote storage.
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                cancellationTokenSource?.Cancel();
                handlerChangesTask?.Wait();

                if (clientWebSocket != null
                    && (clientWebSocket?.State == WebSocketState.Open || clientWebSocket?.State == WebSocketState.CloseSent || clientWebSocket?.State == WebSocketState.CloseReceived))
                {
                    await clientWebSocket?.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
            }
            catch (WebSocketException ex)
            {
                Logger.LogMessage($"Failed to close websocket. {ex.Message}", WebSocketServerUrl, null);
            };

            Logger.LogMessage("Stoped", WebSocketServerUrl);
        }

        /// <summary>
        /// Processes message recieved from the remote storage.
        /// </summary>
        /// <param name="message">Information about changes or null in case of web sockets start/reconnection.</param>
        /// <remarks>
        /// This method is called on each message being received as well as on web sockets connection and reconnection.
        /// 
        /// We do not pass WebSockets cancellation token to this method because stopping 
        /// web sockets should not stop processing changes. 
        /// To stop processing changes that are already received the Engine must be stopped.
        /// </remarks>
        protected abstract Task ProcessAsync(WebSocketMessage message = null);

        /// <summary>
        /// Starts thread that processes changes queue.
        /// </summary>
        /// <param name="cancelationToken">The token to monitor for cancellation requests.</param>
        /// <returns></returns>
        private Task CreateHandlerChangesTask(CancellationToken cancelationToken)
        {
            return Task.Run(async () =>
            {
                try
                {
                    while (!cancelationToken.IsCancellationRequested)
                    {
                        WebSocketMessage message = changeQueue.Take(cancelationToken);
                        await ProcessAsync(message);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            },
            cancelationToken);
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
                    handlerChangesTask?.Wait();
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
