using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using log4net;
using ITHit.FileSystem.Windows;


namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Application commands.
    /// </summary>
    public class Commands
    {
        /// <summary>
        /// Engine instance.
        /// </summary>
        public EngineWindows Engine;

        /// <summary>
        /// Remote storage monitor.
        /// </summary>
        public ISyncService RemoteStorageMonitor;

        /// <summary>
        /// Log4Net logger.
        /// </summary>
        private readonly ILog log;

        /// <summary>
        /// Remote storage root path.
        /// </summary>
        private readonly string remoteStorageRootPath;

        public Commands(ILog log, string remoteStorageRootPath)
        {
            this.log = log;
            this.remoteStorageRootPath = remoteStorageRootPath;
        }

        /// <summary>
        /// Start/stop the Engine and all sync services.
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
        /// Start/stop synchronization service.
        /// </summary>
        public async Task StartStopSynchronizationAsync()
        {
            switch (Engine.SyncService.SyncState)
            {
                case SynchronizationState.Disabled:
                    if (Engine.State != EngineState.Running)
                    {
                        Engine.SyncService.Logger.LogError("Failed to start. The Engine must be running.");
                        return;
                    }
                    await Engine.SyncService.StartAsync();
                    break;

                default:
                    await Engine.SyncService.StopAsync();
                    break;
            }
        }

        public async Task StartStopRemoteStorageMonitorAsync()
        {
            if (RemoteStorageMonitor.SyncState == SynchronizationState.Disabled)
            {
                if (Engine.State != EngineState.Running)
                {
                    log.Error("Failed to start. The Engine must be running.");
                    //Engine.RemoteStorageMonitor.Logger.LogError("Failed to start. The Engine must be running.");
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
        /// Opens path with associated application.
        /// </summary>
        /// <param name="path">Path to the file or folder.</param>
        public static void Open(string path)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(path);
            startInfo.UseShellExecute = true; // Open window only if not opened already.
            using (Process ufsWinFileManager = Process.Start(startInfo))
            {

            }
        }

        /// <summary>
        /// Open Windows File Manager with user file system.
        /// </summary>
        public async Task OpenFolderAsync()
        {
            Open(Engine.Path);
        }

        /// <summary>
        /// Open remote storage.
        /// </summary>
        public async Task OpenRemoteStorageAsync()
        {
            Open(remoteStorageRootPath);
        }

        /// <summary>
        /// Opens support portal.
        /// </summary>
        public async Task OpenSupportPortalAsync()
        {
            Open("https://www.userfilesystem.com/support/");
        }

        /// <summary>
        /// Called on app exit.
        /// </summary>
        public async Task AppExitAsync()
        {
            await StopEngineAsync();
            log.Info("\n\nAll downloaded file / folder placeholders remain in file system. Restart the application to continue managing files.");
            log.Info("\nYou can also edit documents when the app is not running and than start the app to sync all changes to the remote storage.\n");
        }

        /// <summary>
        /// Stop the Engine and all sync services.
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
        /// <remarks>This method is provided solely for the development and testing convenience.</remarks>
        public void ShowTestEnvironment()
        {
            // Enable UTF8 for Console Window and set width.
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.SetWindowSize(Console.LargestWindowWidth, Console.LargestWindowHeight / 3);
            Console.SetBufferSize(Console.LargestWindowWidth * 2, short.MaxValue / 2);

            // Open Windows File Manager with user file system.
            Commands.Open(Engine.Path);

            // Open remote storage.
            Commands.Open(remoteStorageRootPath);
        }
#endif
    }
}
