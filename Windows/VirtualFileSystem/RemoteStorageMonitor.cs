using System;
using System.IO;
using System.Linq;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;
using System.Threading.Tasks;
using System.Threading;

namespace VirtualFileSystem
{
    /// <summary>
    /// Monitors changes in the remote storage, notifies the client and updates the user file system.
    /// If any file or folder is modified, created, delated, renamed or attributes changed in the remote storage, 
    /// this class triggers an event with information about changes.
    /// </summary>
    /// <remarks>
    /// Here, for demo purposes we simulate server by monitoring source file path using FileWatchWrapper.
    /// In your application, instead of using FileWatchWrapper, you will connect to your remote storage using web sockets 
    /// or use any other technology to get notifications about changes in your remote storage.
    /// </remarks>
    public class RemoteStorageMonitor : ISyncService, IDisposable
    {
        /// <summary>
        /// Current synchronization state.
        /// </summary>
        public virtual SynchronizationState SyncState
        {
            get
            {
                return watcher.EnableRaisingEvents ? SynchronizationState.Enabled : SynchronizationState.Disabled;
            }
        }

        /// <summary>
        /// Logger.
        /// </summary>
        public readonly ILogger Logger;

        /// <summary>
        /// Engine instance. We will call <see cref="Engine"/> methods 
        /// to update user file system when any data is changed in the remote storage.
        /// </summary>
        private readonly Engine engine;

        /// <summary>
        /// Watches for changes in the remote storage file system.
        /// </summary>
        private readonly FileSystemWatcherQueued watcher = new FileSystemWatcherQueued();

        private readonly Mapping mapping;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="remoteStorageRootPath">Remote storage path. Folder that contains source files to monitor changes.</param>
        /// <param name="engine">Virtual drive to send notifications about changes in the remote storage.</param>
        /// <param name="logger">Logger.</param>
        internal RemoteStorageMonitor(string remoteStorageRootPath, Engine engine, ILogger logger)
        {
            this.engine = engine;
            this.Logger = logger.CreateLogger("Remote Storage Monitor");

            mapping = new Mapping(engine.Path, remoteStorageRootPath);

            watcher.IncludeSubdirectories = true;
            watcher.Path = remoteStorageRootPath;
            //watcher.Filter = "*.*";
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Attributes | NotifyFilters.DirectoryName | NotifyFilters.FileName;
            watcher.Error += Error;
            watcher.Created += CreatedAsync;
            watcher.Changed += ChangedAsync;
            watcher.Deleted += DeletedAsync;
            watcher.Renamed += RenamedAsync;
            watcher.EnableRaisingEvents = false;
        }

        /// <summary>
        /// Starts monitoring changes on the server.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            watcher.EnableRaisingEvents = true;
            Logger.LogMessage($"Started", watcher.Path);
        }

        /// <summary>
        /// Stops monitoring changes in the remote storage.
        /// </summary>
        public async Task StopAsync()
        {
            watcher.EnableRaisingEvents = false;
            Logger.LogMessage($"Stopped", watcher.Path);
        }

        /// <summary>
        /// Called when a file or folder is created in the remote storage.
        /// </summary>
        /// <remarks>In this method we create a new file/folder in the user file system.</remarks>
        private async void CreatedAsync(object sender, FileSystemEventArgs e)
        {
            Logger.LogDebug($"Operation: {e.ChangeType}", e.FullPath);
            string remoteStoragePath = e.FullPath;

            try
            {
                string userFileSystemPath = mapping.ReverseMapPath(remoteStoragePath);

                // This check is only required because we can not prevent circular calls because of the simplicity of this example.
                // In your real-life application you will not send updates from server back to client that issued the update.
                if (!FsPath.Exists(userFileSystemPath))
                {
                    string userFileSystemParentPath = Path.GetDirectoryName(userFileSystemPath);

                    FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(remoteStoragePath);
                    if (remoteStorageItem != null)
                    {
                        IFileSystemItemMetadata newItemInfo = Mapping.GetUserFileSysteItemMetadata(remoteStorageItem);
                        if (await engine.ServerNotifications(userFileSystemParentPath, Logger).CreateAsync(new[] { newItemInfo }) > 0)
                        {
                            // Because of the on-demand population, the parent folder placeholder may not exist in the user file system
                            // or the folder may be offline. In this case the IServerNotifications.CreateAsync() call is ignored.
                            Logger.LogMessage($"Created successfully", userFileSystemPath);
                        }
                    }
                }
                else
                {
                    ChangedAsync(sender, e);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"{e.ChangeType} failed", remoteStoragePath, null, ex);
            }
        }

        /// <summary>
        /// Called when a file content changed or file/folder attributes changed in the remote storage.
        /// </summary>
        /// <remarks>
        /// In this method we update corresponding file/folder information in user file system.
        /// We also dehydrate the file if it is not blocked.
        /// </remarks>
        private async void ChangedAsync(object sender, FileSystemEventArgs e)
        {
            Logger.LogDebug($"Operation: {e.ChangeType}", e.FullPath);
            string remoteStoragePath = e.FullPath;
            string userFileSystemPath = null;
            try
            {
                userFileSystemPath = mapping.ReverseMapPath(remoteStoragePath);

                // This check is only required because we can not prevent circular calls because of the simplicity of this example.
                // In your real-life application you will not send updates from server back to client that issued the update.
                if (FsPath.Exists(userFileSystemPath)
                    && IsModified(userFileSystemPath, remoteStoragePath))
                {
                    FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(remoteStoragePath);
                    if (remoteStorageItem != null)
                    {
                        IFileSystemItemMetadata itemInfo = Mapping.GetUserFileSysteItemMetadata(remoteStorageItem);

                        if (await engine.ServerNotifications(userFileSystemPath, Logger).UpdateAsync(itemInfo))
                        {
                            // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                            // In this case the IServerNotifications.UpdateAsync() call is ignored.

                            PlaceholderItem.UpdateUI(userFileSystemPath);
                            Logger.LogMessage("Updated successfully", userFileSystemPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // The file is blocked in the user file system, the item is not a pleceholder, etc.
                // Typically this is a normal behaviour.
                Logger.LogDebug($"{e.ChangeType}. {ex.Message}", remoteStoragePath);
            }
        }

        /// <summary>
        /// Called when a file or folder is deleted in the remote storage.
        /// </summary>
        /// <remarks>In this method we delete corresponding file/folder in the user file system.</remarks>
        private async void DeletedAsync(object sender, FileSystemEventArgs e)
        {
            Logger.LogDebug($"Operation: {e.ChangeType}", e.FullPath);

            // Run sync operation asynchronously, because we need some delay to avoid
            // circular calls wich leads to slow operation execution.
            _ = Task.Run(() => ProcessDeletedAsync(e));
        }

        private async Task ProcessDeletedAsync(FileSystemEventArgs e)
        {
            string remoteStoragePath = e.FullPath;
            try
            {
                string userFileSystemPath = mapping.ReverseMapPath(remoteStoragePath);

                // This check and delay is only required because we can not prevent circular calls because of the simplicity of this example.
                // In your real-life application you will not send updates from server back to client that issued the update.
                System.Threading.Thread.Sleep(1000);
                if (FsPath.Exists(userFileSystemPath))
                {
                    // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                    if (await engine.ServerNotifications(userFileSystemPath, Logger).DeleteAsync())
                    {
                        Logger.LogMessage("Deleted successfully", userFileSystemPath);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Logger.LogError($"{e.ChangeType} failed. The item is blocked", remoteStoragePath, null);
            }
            catch (Exception ex)
            {
                Logger.LogError($"{e.ChangeType} failed", remoteStoragePath, null, ex);
            }
        }

        /// <summary>
        /// Called when a file or folder is renamed in the remote storage.
        /// </summary>
        /// <remarks>In this method we rename corresponding file/folder in user file system.</remarks>
        private async void RenamedAsync(object sender, RenamedEventArgs e)
        {
            Logger.LogDebug($"Operation: {e.ChangeType}", e.OldFullPath, e.FullPath);
            string remoteStorageOldPath = e.OldFullPath;
            string remoteStorageNewPath = e.FullPath;
            try
            {
                string userFileSystemOldPath = mapping.ReverseMapPath(remoteStorageOldPath);
                string userFileSystemNewPath = mapping.ReverseMapPath(remoteStorageNewPath);

                // This check is only required because we can not prevent circular calls because of the simplicity of this example.
                // In your real-life application you will not send updates from server back to client that issued the update.
                if (FsPath.Exists(userFileSystemOldPath))
                {
                    if (await engine.ServerNotifications(userFileSystemOldPath, Logger).MoveToAsync(userFileSystemNewPath))
                    {
                        // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                        // In this case the IServerNotifications.MoveToAsync() call is ignored.
                        Logger.LogMessage("Renamed successfully", userFileSystemOldPath, userFileSystemNewPath);
                    }
                }

                // Possibly the item was filtered by MS Office filter because of the transactional save.
                // The target item should be updated in this case.
                // This call, is onlly required to support MS Office editing in the folder simulating remote storage.
                ChangedAsync(sender, e);
            }
            catch (Exception ex)
            {
                Logger.LogError($"{e.ChangeType} failed", remoteStorageOldPath, remoteStorageNewPath, ex);
            }
        }

        private void Error(object sender, ErrorEventArgs e)
        {
            Logger.LogError(null, null, null, e.GetException());
        }


        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    watcher.Dispose();
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

        /// <summary>
        /// Compares two files contents.
        /// </summary>
        /// <param name="userFileSystemPath">File or folder 1 to compare.</param>
        /// <param name="remoteStoragePath">File or folder 2 to compare.</param>
        /// <returns>True if file is modified. False - otherwise.</returns>
        internal static bool IsModified(string userFileSystemPath, string remoteStoragePath)
        {
            if (FsPath.IsFolder(userFileSystemPath) && FsPath.IsFolder(remoteStoragePath))
            {
                return false;
            }

            FileInfo fiUserFileSystem = new FileInfo(userFileSystemPath);
            FileInfo fiRemoteStorage = new FileInfo(remoteStoragePath);

            // This check is to prevent circular calls. In your real app you would not send notifications to the client that generated the event.
            if (fiUserFileSystem.LastWriteTimeUtc >= fiRemoteStorage.LastWriteTimeUtc)
            {
                return false;
            }

            try
            {
                if (fiUserFileSystem.Length == fiRemoteStorage.Length)
                {
                    // Verify that the file is not offline,
                    // otherwise the file will be hydrated when the file stream is opened.
                    if (fiUserFileSystem.Attributes.HasFlag(System.IO.FileAttributes.Offline)
                        || fiUserFileSystem.Attributes.HasFlag(System.IO.FileAttributes.Offline))
                    {
                        return false;
                    }

                    byte[] hash1;
                    byte[] hash2;
                    using (var alg = System.Security.Cryptography.MD5.Create())
                    {
                        // This code for demo purposes only. We do not block files for writing, which is required by some apps, for example by AutoCAD.
                        using (FileStream stream = new FileStream(userFileSystemPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        {
                            hash1 = alg.ComputeHash(stream);
                        }
                        using (FileStream stream = new FileStream(remoteStoragePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        {
                            hash2 = alg.ComputeHash(stream);
                        }
                    }

                    return !hash1.SequenceEqual(hash2);
                }
            }
            catch (IOException)
            {
                // One of the files is blocked. Can not compare files and start sychronization.
                return false;
            }

            return true;
        }
    }
}
