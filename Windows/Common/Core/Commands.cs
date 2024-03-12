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
    /// Commands sent from tray app and comnsole.
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
        /// Remote storaage path.
        /// </summary>
        private readonly string RemoteStorageRootPath;

        /// <summary>
        /// Log4Net logger.
        /// </summary>
        private readonly ILog log;

        public Commands(EngineWindows engine, string remoteStorageRootPath, ILog log)
        {
            this.Engine = engine;
            this.RemoteStorageRootPath = remoteStorageRootPath;
            this.log = log;
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

        public async Task StartStopRemoteStorageMonitorAsync()
        {
            if(RemoteStorageMonitor == null)
            {
                Engine.Logger.LogError("Remote storage monitor is null.", Engine.Path);
                return;
            }

            if (RemoteStorageMonitor.SyncState == SynchronizationState.Disabled)
            {
                if (Engine.State != EngineState.Running)
                {
                    Engine.Logger.LogError("Failed to start. The Engine must be running.", Engine.Path);
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
        /// Open root user file system folder in Windows Explorer.
        /// </summary>
        public async Task OpenRootFolderAsync()
        {
            Open(Engine.Path);
        }

        /// <summary>
        /// Open remote storage.
        /// </summary>
        public async Task OpenRemoteStorageAsync()
        {
            Open(RemoteStorageRootPath);
        }

        /// <summary>
        /// Opens support portal.
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
            log.Info("\nYou can edit documents when the app is not running and than start the app to sync all changes to the remote storage.\n");
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
        /// <param name="openRemoteStorage">True if the Remote Storage must be opened. False - otherwise.</param>
        /// <remarks>This method is provided solely for the development and testing convenience.</remarks>
        public void ShowTestEnvironment(string userFileSystemWindowName, bool openRemoteStorage = true, CancellationToken cancellationToken = default)
        {
            // Open Windows File Manager with user file system.
            Commands.Open(Engine.Path);
            IntPtr hWndUserFileSystem = WindowManager.FindWindow(userFileSystemWindowName, cancellationToken);
            WindowManager.PositionFileSystemWindow(hWndUserFileSystem, 1, 2);

            if (openRemoteStorage)
            {
                // Open remote storage.
                Commands.Open(RemoteStorageRootPath);
                string rsWindowName = Path.GetFileName(RemoteStorageRootPath.TrimEnd('\\'));
                IntPtr hWndRemoteStorage = WindowManager.FindWindow(rsWindowName, cancellationToken);
                WindowManager.PositionFileSystemWindow(hWndRemoteStorage, 0, 2);
            }
        }

#endif

        public void Test()
        {
            string name = "Notes.txt";
            string filePath = Path.Combine(Engine.Path, name);
            //FileInfo fi = new FileInfo(filePath);
            //fi.IsReadOnly = true;

            var n = Engine.ServerNotifications(filePath);
            IFileMetadata metadata = new FileMetadata();
            metadata.Attributes = FileAttributes.Normal;
            metadata.CreationTime = DateTimeOffset.Now;
            metadata.LastWriteTime = DateTimeOffset.Now;
            metadata.ChangeTime = DateTimeOffset.Now;
            metadata.LastAccessTime = DateTimeOffset.Now;
            metadata.Name = name;
            metadata.MetadataETag = DateTimeOffset.Now.Ticks.ToString();
            metadata.ContentETag = null;//"etag1";
            n.UpdateAsync(metadata);
        }

    }
}
