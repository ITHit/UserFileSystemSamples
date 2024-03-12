using System;
using System.IO;
using Common.Core;
using ITHit.FileSystem;

namespace VirtualFilesystemMacApp
{
    /// <summary>
    /// Monitors changes in the remote storage, notifies the client and updates the user file system.
    /// If any file or folder is is modified, created, delated, renamed or attributes changed in the remote storage, 
    /// triggers an event with information about changes being made.
    /// </summary>
    /// <remarks>
    /// Here, for demo purposes we simulate server by monitoring source file path using FileSystemWatcher.
    /// In your application, instead of using FileSystemWatcher, you will connect to your remote storage using web sockets 
    /// or use any other technology to get notifications about changes in your remote storage.
    /// </remarks>
    public class RemoteStorageMonitor: IDisposable
    {
        /// <summary>
        /// Remote storage path. Folder to monitor for changes.
        /// </summary>
        private string remoteStorageRootPath;

        /// <summary>
        /// <see cref="ILogger"/> instance.
        /// </summary>
        public readonly ILogger Logger;

        /// <summary>
        /// Watches for changes in the remote storage file system.
        /// </summary>
        private FileSystemWatcher watcher = new FileSystemWatcher();

        /// <summary>
        /// Server notifications will be sent to this object.
        /// </summary>
        public IServerNotifications ServerNotifications;

        public RemoteStorageMonitor(string remoteStorageRootPath, ILogger logger)
        {
            this.remoteStorageRootPath = remoteStorageRootPath;
            this.Logger = logger;
        }

        /// <summary>
        /// Starts monitoring changes on the server.
        /// </summary>
        public void Start()
        {
            watcher.IncludeSubdirectories = true;
            watcher.Path = remoteStorageRootPath;
            //watcher.Filter = "*.*";
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Attributes | NotifyFilters.DirectoryName | NotifyFilters.FileName;
            watcher.Error += Error;
            watcher.Created += CreatedAsync;
            watcher.Changed += ChangedAsync;
            watcher.Deleted += DeletedAsync;
            watcher.Renamed += RenamedAsync;
            watcher.EnableRaisingEvents = true;

            Logger.LogMessage($"Remote Storage Started", remoteStorageRootPath);
        }

        /// <summary>
        /// Stops monitoring changes in the remote storage.
        /// </summary>
        public void Stop()
        {
            watcher.EnableRaisingEvents = false;
            Logger.LogMessage($"Remote Storage Stoped", remoteStorageRootPath);
        }

        private void RenamedAsync(object sender, RenamedEventArgs e)
        {
            Logger.LogMessage(e.ChangeType.ToString(), e.FullPath);

            ServerNotifications.MoveToAsync(e.FullPath);
        }

        private void DeletedAsync(object sender, FileSystemEventArgs e)
        {
            Logger.LogMessage(e.ChangeType.ToString(), e.FullPath);

            ServerNotifications.DeleteAsync();
        }

        private void CreatedAsync(object sender, FileSystemEventArgs e)
        {
            Logger.LogMessage(e.ChangeType.ToString(), e.FullPath);

            ServerNotifications.CreateAsync(null);
        }

        private void ChangedAsync(object sender, FileSystemEventArgs e)
        {
            Logger.LogMessage(e.ChangeType.ToString(), e.FullPath);

            ServerNotifications.UpdateAsync(null);
        }

        private void Error(object sender, ErrorEventArgs e)
        {
            Logger.LogError(null, ex: e.GetException());
        }

        public void Dispose()
        {
            watcher.Dispose();
        }
    }
}
