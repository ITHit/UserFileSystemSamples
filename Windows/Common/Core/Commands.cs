using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using log4net;
using ITHit.FileSystem.Windows;
using System.IO;
using System.Collections.Concurrent;
using System.Threading;


namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Commands sent from tray app and console.
    /// </summary>
    public class Commands
    {
        /// <summary>
        /// Engine instance.
        /// </summary>
        private readonly EngineWindows Engine;

        /// <summary>
        /// Remote storage monitor.
        /// </summary>
        public ISyncService RemoteStorageMonitor;

        /// <summary>
        /// Remote storage path.
        /// </summary>
        private readonly string RemoteStorageRootPath;

        /// <summary>
        /// Log4Net logger.
        /// </summary>
        private readonly ILog log;

        /// <summary>
        /// Initializes a new instance of the <see cref="Commands"/> class.
        /// </summary>
        /// <param name="engine">The engine instance.</param>
        /// <param name="remoteStorageRootPath">The remote storage root path.</param>
        /// <param name="log">The logger instance.</param>
        public Commands(EngineWindows engine, string remoteStorageRootPath, ILog log)
        {
            this.Engine = engine;
            this.RemoteStorageRootPath = remoteStorageRootPath;
            this.log = log;
        }

        /// <summary>
        /// Start or stop the Engine and all sync services.
        /// </summary>
        public async Task StartStopEngineAsync()
        {
            switch (Engine.State)
            {
                case EngineState.Running:
                    await Engine.StopAsync();
                    break;

                case EngineState.Stopped:
                    await Engine.StartAsync();
                    break;
            }
        }

        /// <summary>
        /// Start or stop the synchronization service.
        /// </summary>
        public async Task StartStopSynchronizationAsync()
        {
            switch (Engine.SyncService.SyncState)
            {
                case SynchronizationState.Disabled:
                    if (Engine.State != EngineState.Running)
                    {
                        Engine.SyncService.Logger.LogError("Failed to start. The Engine must be running.", Engine.Path);
                        return;
                    }
                    await Engine.SyncService.StartAsync();
                    break;

                default:
                    await Engine.SyncService.StopAsync();
                    break;
            }
        }

        /// <summary>
        /// Start or stop the remote storage monitor.
        /// </summary>
        public async Task StartStopRemoteStorageMonitorAsync()
        {
            if (RemoteStorageMonitor == null)
            {
                Engine.Logger.LogError("Remote storage monitor is null.", Engine.Path);
                return;
            }

            if (RemoteStorageMonitor.SyncState == SynchronizationState.Disabled)
            {
                if (Engine.State != EngineState.Running)
                {
                    Engine.Logger.LogError("Failed to start. The Engine must be running.", Engine.Path);
                    return;
                }
                await RemoteStorageMonitor.StartAsync();
            }
            else
            {
                await RemoteStorageMonitor.StopAsync();
            }
        }

        /// <summary>
        /// Opens the specified path with the associated application.
        /// </summary>
        /// <param name="path">The path to the file or folder.</param>
        public static void Open(string path)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(path)
            {
                UseShellExecute = true // Open window only if not opened already.
            };
            using (Process ufsWinFileManager = Process.Start(startInfo))
            {
            }
        }

        /// <summary>
        /// Tries to open the specified path.
        /// </summary>
        /// <param name="path">The path to the file or folder.</param>
        /// <returns>True if the path was opened successfully, otherwise false.</returns>
        public bool TryOpen(string path)
        {
            return TryOpen(path, log);
        }

        /// <summary>
        /// Tries to open the specified path.
        /// </summary>
        /// <param name="path">The path to the file or folder.</param>
        /// <returns>True if the path was opened successfully, otherwise false.</returns>
        public static bool TryOpen(string path, ILog? log = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && (File.Exists(path) || Directory.Exists(path)))
                {
                    Open(path);
                    return true;
                }
                else
                {
                    log?.Warn($"The path {path} does not exist.");
                }
            }
            catch (Exception ex)
            {
                log?.Error($"Failed to open {path}.", ex);
            }

            return false;
        }

        /// <summary>
        /// Opens the support portal.
        /// </summary>
        public static async Task OpenSupportPortalAsync()
        {
            Open("https://www.userfilesystem.com/support/");
        }

        /// <summary>
        /// Called on app exit.
        /// </summary>
        public async Task EngineExitAsync()
        {
            await StopEngineAsync();
            log.Info($"\n\n{RemoteStorageRootPath}");
            log.Info("\nAll downloaded file / folder placeholders remain in file system. Restart the application to continue managing files.");
            log.Info("\nYou can edit documents when the app is not running and then start the app to sync all changes to the remote storage.\n");
        }

        /// <summary>
        /// Stops the Engine and all sync services.
        /// </summary>
        public async Task StopEngineAsync()
        {
            if (Engine?.State == EngineState.Running)
            {
                await Engine.StopAsync();
            }
        }

#if DEBUG
        /// <summary>
        /// Opens Windows File Manager with both remote storage and user file system for testing.
        /// </summary>
        /// <param name="openRemoteStorage">True if the Remote Storage must be opened. False otherwise.</param>
        /// <param name="engineIndex">Index used to position Windows Explorer window to show this user file system.</param>
        /// <param name="totalEngines">Total number of Engines that will be mounted by this app.</param>
        /// <param name="userFileSystemWindowName">Name of the user file system window.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>This method is provided solely for the development and testing convenience.</remarks>
        public void ShowTestEnvironment(string userFileSystemWindowName, bool openRemoteStorage = true, CancellationToken cancellationToken = default, int engineIndex = 0, int totalEngines = 1)
        {
            int numWindowsPerEngine = 2; // Each engine shows 2 windows - remote storage and UFS.
            int horizontalIndex = engineIndex * numWindowsPerEngine;
            int totalWindows = totalEngines * numWindowsPerEngine;

            // Open remote storage.
            if (openRemoteStorage)
            {
                TryOpen(RemoteStorageRootPath);
            }

            // Open Windows File Manager with user file system.
            TryOpen(Engine.Path);
        }
#endif
    }
}
