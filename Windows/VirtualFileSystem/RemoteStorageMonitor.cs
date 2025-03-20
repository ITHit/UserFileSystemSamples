using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;


namespace VirtualFileSystem
{
    /// <summary>
    /// Monitors changes in the remote storage, notifies the client and updates the user file system.
    /// If any file or folder is modified, created, delated, renamed or attributes changed in the remote storage, 
    /// this class triggers an event with information about changes.
    /// </summary>
    /// <remarks>
    /// Here, for demo purposes we simulate server by monitoring source file path using file system watcher.
    /// In your application, you will connect to your remote storage using web sockets or similar technology.
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
        /// Engine instance. We will call <see cref="Engine"/> methods 
        /// to update user file system when any data is changed in the remote storage.
        /// </summary>
        private readonly Engine engine;

        /// <summary>
        /// Logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Watches for changes in the remote storage file system.
        /// </summary>
        private readonly FileSystemWatcherQueued watcher = new FileSystemWatcherQueued();

        /// <summary>
        /// Maps a the remote storage path and data to the user file system path and data.  
        /// </summary>
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
            this.logger = logger.CreateLogger("Remote Storage Monitor");
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
            logger.LogMessage($"Started", watcher.Path);
        }

        /// <summary>
        /// Stops monitoring changes in the remote storage.
        /// </summary>
        public async Task StopAsync()
        {
            watcher.EnableRaisingEvents = false;
            logger.LogMessage($"Stopped", watcher.Path);
        }

        /// <summary>
        /// Called when a file or folder is created in the remote storage.
        /// </summary>
        /// <remarks>In this method we create a new file/folder in the user file system.</remarks>
        private async void CreatedAsync(object sender, FileSystemEventArgs e)
        {
            logger.LogDebug($"Operation: {e.ChangeType}", e.FullPath);
            string remoteStoragePath = e.FullPath;

            string userFileSystemPath = mapping.ReverseMapPath(remoteStoragePath);
            string userFileSystemParentPath = Path.GetDirectoryName(userFileSystemPath);

            FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(remoteStoragePath);
            if (remoteStorageItem != null)
            {
                IMetadata itemMetadata = Mapping.GetMetadata(remoteStorageItem);
                await engine.ServerNotifications(userFileSystemParentPath, logger).CreateAsync(new[] { itemMetadata });
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
            logger.LogDebug($"Operation: {e.ChangeType}", e.FullPath);
            string remoteStoragePath = e.FullPath;
            string userFileSystemPath = null;

            userFileSystemPath = mapping.ReverseMapPath(remoteStoragePath);

            // This check is only required because we can not prevent circular calls because of the simplicity of this example.
            // In your real-life application you will not send updates from server back to client that issued the update.
            if (IsModified(userFileSystemPath, remoteStoragePath))
            {
                FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(remoteStoragePath);
                if (remoteStorageItem != null)
                {
                    IMetadata itemMetadata = Mapping.GetMetadata(remoteStorageItem);
                    await engine.ServerNotifications(userFileSystemPath, logger).UpdateAsync(itemMetadata);
                }
            }
        }

        /// <summary>
        /// Called when a file or folder is deleted in the remote storage.
        /// </summary>
        /// <remarks>In this method we delete corresponding file/folder in the user file system.</remarks>
        private async void DeletedAsync(object sender, FileSystemEventArgs e)
        {
            logger.LogDebug($"Operation: {e.ChangeType}", e.FullPath);
            string remoteStoragePath = e.FullPath;
            string userFileSystemPath = mapping.ReverseMapPath(remoteStoragePath);
            await engine.ServerNotifications(userFileSystemPath, logger).DeleteAsync();
        }

        /// <summary>
        /// Called when a file or folder is renamed in the remote storage.
        /// </summary>
        /// <remarks>In this method we rename corresponding file/folder in user file system.</remarks>
        private async void RenamedAsync(object sender, RenamedEventArgs e)
        {
            logger.LogDebug($"Operation: {e.ChangeType}", e.OldFullPath, e.FullPath);
            string remoteStorageOldPath = e.OldFullPath;
            string remoteStorageNewPath = e.FullPath;

            string userFileSystemOldPath = mapping.ReverseMapPath(remoteStorageOldPath);
            string userFileSystemNewPath = mapping.ReverseMapPath(remoteStorageNewPath);

            await engine.ServerNotifications(userFileSystemOldPath, logger).MoveToAsync(userFileSystemNewPath);

            // Possibly the item was filtered by MS Office filter because of the transactional save.
            // The target item should be updated in this case.
            // This call, is onlly required to support MS Office editing in the folder simulating remote storage.
            ChangedAsync(sender, e);
        }

        private void Error(object sender, ErrorEventArgs e)
        {
            logger.LogError(null, null, null, e.GetException());
        }


        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    watcher.Dispose();
                    logger.LogMessage($"Disposed");
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
            if (!FsPath.TryIsFolder(userFileSystemPath, out bool isFolder1) || isFolder1 || !FsPath.TryIsFolder(remoteStoragePath, out bool isFolder2) || isFolder2)
            {
                return false;
            }
            
            try
            {
                FileInfo fiUserFileSystem = new FileInfo(userFileSystemPath);
                FileInfo fiRemoteStorage = new FileInfo(remoteStoragePath);

                // This check is to prevent circular calls. In your real app you would not send notifications to the client that generated the event.
                if (fiUserFileSystem.LastWriteTimeUtc >= fiRemoteStorage.LastWriteTimeUtc)
                {
                    return false;
                }

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
