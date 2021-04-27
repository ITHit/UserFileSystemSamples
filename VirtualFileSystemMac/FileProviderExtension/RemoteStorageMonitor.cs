using System;
using System.IO;
using System.Threading.Tasks;
using FileProvider;

namespace FileProviderExtension
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
    public class RemoteStorageMonitor
    {
        /// <summary>
        /// Remote storage path. Folder to monitor for changes.
        /// </summary>
        private string remoteStorageRootPath;

        /// <summary>
        /// <see cref="ConsoleLogger"/> instance.
        /// </summary>
        private readonly ConsoleLogger logger;

        /// <summary>
        /// Watches for changes in the remote storage file system.
        /// </summary>
        private FileSystemWatcher watcher = new FileSystemWatcher();

        /// <summary>
        /// File provider manager for signals an update to virtual system.
        /// </summary>
        private NSFileProviderManager fileProviderManager;

        public RemoteStorageMonitor(string remoteStorageRootPath, NSFileProviderManager fileProviderManager)
        {
            this.remoteStorageRootPath = remoteStorageRootPath;
            this.logger = new ConsoleLogger(GetType().Name);
            this.fileProviderManager = fileProviderManager;
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

            logger.LogMessage($"Remote Storage Started", remoteStorageRootPath);
        }

        /// <summary>
        /// Stops monitoring changes in the remote storage.
        /// </summary>
        public void Stop()
        {
            watcher.EnableRaisingEvents = false;
            logger.LogMessage($"Remote Storage Stoped", remoteStorageRootPath);
        }

        private void RenamedAsync(object sender, RenamedEventArgs e)
        {
            logger.LogMessage(e.ChangeType.ToString(), e.FullPath);
        }

        private void DeletedAsync(object sender, FileSystemEventArgs e)
        {
            logger.LogMessage(e.ChangeType.ToString(), e.FullPath);
        }

        private void CreatedAsync(object sender, FileSystemEventArgs e)
        {
            logger.LogMessage(e.ChangeType.ToString(), e.FullPath);
            string userFileSystemParentPath = Path.GetDirectoryName(e.FullPath);
            logger.LogMessage(e.ChangeType.ToString(), Mapping.ReverseMapPath(userFileSystemParentPath));

            fileProviderManager.SignalEnumerator(/*NSFileProviderItemIdentifier.RootContainer*/Mapping.ReverseMapPath(userFileSystemParentPath), error => {
                if (error != null)
                {
                    logger.LogError(error.Description);
                }
            });

            
        }

        private void ChangedAsync(object sender, FileSystemEventArgs e)
        {
            logger.LogMessage(e.ChangeType.ToString(), e.FullPath);
            string remoteStoragePath = e.FullPath;

            //fileProviderManager.SignalEnumerator(Mapping.ReverseMapPath(remoteStoragePath), error => {
            //    if (error != null)
            //    {
            //        logger.LogError(error.Description);
            //    }
            //});            
        }

        private void Error(object sender, ErrorEventArgs e)
        {
            logger.LogError(null, ex: e.GetException());
        }
    }
}
