using System;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Core;
using FileProvider;
using WebDAVCommon;

namespace WebDAVMacApp
{
    /// <summary>
    /// Monitors changes in the remote storage, notifies the client and updates the user file system.
    /// If any file or folder is created, updated, delated, moved, locked or unlocked in the remote storage, 
    /// triggers an event with information about changes being made.
    /// </summary>
    public class RemoteStorageMonitor
    {
        /// <summary>
        /// File provider manager.
        /// </summary>
        private readonly NSFileProviderManager fileProviderManager;

        /// <summary>
        /// WebSocket server url.
        /// </summary>
        private readonly string webSocketServerUrl;

        /// <summary>
        /// <see cref="ConsoleLogger"/> instance.
        /// </summary>
        private readonly ConsoleLogger logger;

        /// <summary>
        /// WebSocket cancellation token.
        /// </summary>
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// WebSocket client.
        /// </summary>
        private ClientWebSocket clientWebSocket;


        public RemoteStorageMonitor(NSFileProviderDomain domain)
        {
            this.fileProviderManager = NSFileProviderManager.FromDomain(domain); 
            this.webSocketServerUrl = AppGroupSettings.GetWebSocketServerUrl();
            this.logger = new ConsoleLogger(GetType().Name);
        }

        /// <summary>
        /// Starts monitoring changes on the server.
        /// </summary>
        public async Task StartAsync()
        {
            cancellationTokenSource = new CancellationTokenSource();
            clientWebSocket = new ClientWebSocket();
            clientWebSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);

            await clientWebSocket.ConnectAsync(new Uri(webSocketServerUrl), CancellationToken.None);

            logger.LogMessage("Started", webSocketServerUrl);

            var rcvBuffer = new ArraySegment<byte>(new byte[2048]);
            while (true)
            {
                WebSocketReceiveResult rcvResult = await clientWebSocket.ReceiveAsync(rcvBuffer, cancellationTokenSource.Token);
                byte[] msgBytes = rcvBuffer.Skip(rcvBuffer.Offset).Take(rcvResult.Count).ToArray();
                string rcvMsg = Encoding.UTF8.GetString(msgBytes);

                fileProviderManager.SignalEnumerator(NSFileProviderItemIdentifier.WorkingSetContainer, error => {
                    if (error != null)
                    {
                        logger.LogError(error.Description);
                    }
                });
            }
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
                    && (clientWebSocket?.State == WebSocketState.Open ||
                        clientWebSocket?.State == WebSocketState.CloseSent ||
                        clientWebSocket?.State == WebSocketState.CloseReceived))
                {
                    await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
            }
            catch (WebSocketException ex)
            {
                logger.LogError("Failed to close websocket.", webSocketServerUrl, null, ex);
            };

            logger.LogMessage("Stoped", webSocketServerUrl);
        }
    }
}
