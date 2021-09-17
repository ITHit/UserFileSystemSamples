using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Provider;
using Windows.System.Update;
using log4net;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;
using System.Net.WebSockets;
using System.Linq;

namespace WebDAVDrive
{
    /// <summary>
    /// Monitors changes in the remote storage, notifies the client and updates the user file system.
    /// If any file or folder is modified, created, delated, renamed or attributes changed in the remote storage, 
    /// triggers an event with information about changes being made.
    /// </summary>
    internal class RemoteStorageMonitor : Logger, IDisposable
    {
        /// <summary>
        /// Logger.
        /// </summary>
        private readonly ILog log;

        /// <summary>
        /// WebSocket client.
        /// </summary>
        private readonly ClientWebSocket clientWebSocket;

        /// <summary>
        /// WebSocket server url.
        /// </summary>
        private readonly string webSocketServerUrl;

        /// <summary>
        /// WebSocket cancellation token.
        /// </summary>
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// Virtul drive instance. This class will call <see cref="Engine"/> methods 
        /// to update user file system when any data is changed in the remote storage.
        /// </summary>
        private readonly VirtualEngine engine;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="webSocketServerUrl">WebSocket server url.</param>
        /// <param name="engine">Engine to send notifications about changes in the remote storage.</param>
        /// <param name="log">Logger.</param>
        internal RemoteStorageMonitor(string webSocketServerUrl, VirtualEngine engine, ILog log) : base("Remote Storage Monitor", log)
        {
            this.clientWebSocket = new ClientWebSocket();
            this.webSocketServerUrl = webSocketServerUrl;
            this.engine = engine;
            this.log = log;
        }

        /// <summary>
        /// Starts monitoring changes in the remote storage.
        /// </summary>
        internal async Task StartAsync()
        {
            cancellationTokenSource = new CancellationTokenSource();
            await clientWebSocket.ConnectAsync(new Uri(webSocketServerUrl), CancellationToken.None);

            await Task.Factory.StartNew(
              async () =>
              {
                  var rcvBytes = new byte[128];
                  var rcvBuffer = new ArraySegment<byte>(rcvBytes);
                  while (true)
                  {
                      WebSocketReceiveResult rcvResult = await clientWebSocket.ReceiveAsync(rcvBuffer, cancellationTokenSource.Token);
                      byte[] msgBytes = rcvBuffer.Skip(rcvBuffer.Offset).Take(rcvResult.Count).ToArray();
                      string rcvMsg = Encoding.UTF8.GetString(msgBytes);
                      log.Info($"\nRemote Storage Monitor Received: {rcvMsg}");
                  }
              }, cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// Stops monitoring changes in the remote storage.
        /// </summary>
        internal async Task StopAsync()
        {
            log.Info($"\nUser File System Monitor Stoping");

            cancellationTokenSource.Cancel();
            if (clientWebSocket.State != WebSocketState.Closed)
            {   
                await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);           
            }

            log.Info($"\nUser File System Monitor Stoped");
        }

        private void Error(object sender, ErrorEventArgs e)
        {
            LogError(null, null, null, e.GetException());
        }


        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {        
                    clientWebSocket.Dispose();
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
}
