using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WebDAVDrive.UI
{
    /// <summary>
    /// UI thread.
    /// </summary>
    public class TrayUI : IDisposable//, ISyncService
    {
        private bool disposedValue;

        private static readonly BlockingCollection<TrayData> newTraysData = new BlockingCollection<TrayData>(new ConcurrentQueue<TrayData>());

        private static ConcurrentDictionary<Guid, WindowsTrayInterface> trays = new ConcurrentDictionary<Guid, WindowsTrayInterface>();

        public class TrayData
        {
            public string ProductName;
            public string WebDavServerPath;
            public string IconsFolderPath;
            public Commands Commands;
            public EngineWindows Engine;
            public Guid InstanceId;
        }

        /// <summary>
        /// Creates a new tray application instance.
        /// </summary>s
        /// <param name="productName">Product name.</param>
        /// <param name="iconsFolderPath">Path to the icons folder.</param>
        /// <param name="commands">Engine commands.</param>
        /// <param name="engine">Engine instance. The tray app will start and stop this instance as well as will display its status.</param>
        /// <returns></returns>
        public static void CreateTray(string productName, string webDavServerPath, string iconsFolderPath, Commands commands, EngineWindows engine, Guid instanceId)
        {
            TrayData trayData = new TrayData
            {
                ProductName = productName,
                WebDavServerPath = webDavServerPath,
                IconsFolderPath = iconsFolderPath,
                Commands = commands,
                Engine = engine,
                InstanceId = instanceId
            };
            newTraysData.Add(trayData);
        }

        public static void RemoveTray(Guid instanceId)
        {
            trays.TryRemove(instanceId, out var tray);
            tray?.Dispose();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            Task.Run(() => { StartProcessingTraysQueue(cancellationToken); }, cancellationToken);
        }

        private static void StartProcessingTraysQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TrayData trayData = newTraysData.Take(cancellationToken); // Blocks if the queue is empty.
                Task.Run(() =>
                {
                    WindowsTrayInterface windowsTrayInterface = new WindowsTrayInterface(trayData.ProductName, trayData.WebDavServerPath, trayData.IconsFolderPath, trayData.Commands);
                    trays.TryAdd(trayData.InstanceId, windowsTrayInterface);

                    // Listen to engine notifications to change menu and icon states.
                    trayData.Engine.StateChanged += windowsTrayInterface.Engine_StateChanged;
                    trayData.Engine.SyncService.StateChanged += windowsTrayInterface.SyncService_StateChanged;

                    Application.Run();
                });
            }

            // Clear the queue.
            while (newTraysData.TryTake(out _))
            {
            }
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    newTraysData?.Dispose();

                    foreach (var tray in trays.Values)
                    {
                        tray?.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~WindowsTrayInterface()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
