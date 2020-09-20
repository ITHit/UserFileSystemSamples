using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using log4net;
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

namespace VirtualFileSystem.Syncronyzation
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
    internal class RemoteStorageMonitor : IDisposable
    {
        /// <summary>
        /// Watches for changes remote storage file system.
        /// </summary>
        private FileSystemWatcher watcher = new FileSystemWatcher();

        /// <summary>
        /// Remote storage path. Folder to minitor changes in.
        /// </summary>
        private string remoteStorageRootPath;

        /// <summary>
        /// Logger.
        /// </summary>
        private ILog log;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="remoteStorageRootPath">Remote storage path. Folder that contains source files to monitor changes.</param>
        /// <param name="log">Logger.</param>
        internal RemoteStorageMonitor(string remoteStorageRootPath, ILog log)
        {
            this.remoteStorageRootPath = remoteStorageRootPath;
            this.log = log;
        }

        /// <summary>
        /// Starts monitoring changes on the server.
        /// </summary>
        internal async Task StartAsync()
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
            
            LogMessage($"Started");
        }

        /// <summary>
        /// Disables or enables monitoring. Used to avoid circular calls.
        /// </summary>
        /// <remarks>
        /// In this sample we can not detect which client have made an update in the remote storage 
        /// folder and have to disable remote storage monitoring when the update is made. In your 
        /// real-life system you do not send requests back to the client that initiated the change 
        /// and will just delete this property.
        /// </remarks>
        internal bool Enabled
        {
            get { return watcher.EnableRaisingEvents; }
            set { watcher.EnableRaisingEvents = value; }
        }

        /// <summary>
        /// Called when a file or folder is created in the remote storage.
        /// </summary>
        /// <remarks>In this method we create a new file/folder in user file system.</remarks>
        private async void CreatedAsync(object sender, FileSystemEventArgs e)
        {
            LogMessage(e.ChangeType.ToString(), e.FullPath);
            string remoteStoragePath = e.FullPath;
            try
            {
                string userFileSystemPath = Mapping.ReverseMapPath(remoteStoragePath);
                string userFileSystemParentPath = Path.GetDirectoryName(userFileSystemPath);

                // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                // Here we also check that the folder content was loaded into user file system (the folder is not offline).
                if (Directory.Exists(userFileSystemParentPath)
                    && !new DirectoryInfo(userFileSystemParentPath).Attributes.HasFlag(System.IO.FileAttributes.Offline))
                {
                    if (!FsPath.AvoidSync(remoteStoragePath))
                    {
                        FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(remoteStoragePath);
                        await UserFileSystemItem.CreateAsync(userFileSystemParentPath, remoteStorageItem);
                        LogMessage("Created succesefully:", userFileSystemPath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"{e.ChangeType} failed:", remoteStoragePath, ex);
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
            LogMessage(e.ChangeType.ToString(), e.FullPath);
            string remoteStoragePath = e.FullPath;
            try
            {
                string userFileSystemPath = Mapping.ReverseMapPath(remoteStoragePath);
                
                // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                if (FsPath.Exists(userFileSystemPath))
                {
                    FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(remoteStoragePath);

                    if (!FsPath.AvoidSync(remoteStoragePath))
                    {
                        // This check is only required because we can not prevent circular calls because of the simplicity of this example.
                        if (!await new UserFileSystemItem(userFileSystemPath).EqualsAsync(remoteStorageItem))
                        {
                            LogMessage("Item modified:", remoteStoragePath);
                            await new UserFileSystemItem(userFileSystemPath).UpdateAsync(remoteStorageItem);
                            LogMessage("Updated succesefully:", userFileSystemPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"{e.ChangeType} failed:", remoteStoragePath, ex);
            }
        }

        /// <summary>
        /// Called when a file or folder is deleted in the remote storage.
        /// </summary>
        /// <remarks>In this method we delete corresponding file/folder in user file system.</remarks>
        private async void DeletedAsync(object sender, FileSystemEventArgs e)
        {
            LogMessage(e.ChangeType.ToString(), e.FullPath);
            string remoteStoragePath = e.FullPath;
            try
            {
                string userFileSystemPath = Mapping.ReverseMapPath(remoteStoragePath);

                // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                if (FsPath.Exists(userFileSystemPath))
                {
                    if (!FsPath.AvoidSync(remoteStoragePath))
                    {
                        await new UserFileSystemItem(userFileSystemPath).DeleteAsync();
                        LogMessage("Deleted succesefully:", userFileSystemPath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"{e.ChangeType} failed:", remoteStoragePath, ex);
            }
        }

        /// <summary>
        /// Called when a file or folder is renamed in the remote storage.
        /// </summary>
        /// <remarks>In this method we rename corresponding file/folder in user file system.</remarks>
        private async void RenamedAsync(object sender, RenamedEventArgs e)
        {
            LogMessage("Renamed:", e.OldFullPath, e.FullPath);
            string remoteStorageOldPath = e.OldFullPath;
            string remoteStorageNewPath = e.FullPath;
            try 
            {
                string userFileSystemOldPath = Mapping.ReverseMapPath(remoteStorageOldPath);

                // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                if (FsPath.Exists(userFileSystemOldPath))
                {
                    if (!FsPath.AvoidSync(remoteStorageOldPath) && !FsPath.AvoidSync(remoteStorageNewPath))
                    {
                        string userFileSystemNewPath = Mapping.ReverseMapPath(remoteStorageNewPath);
                        await new UserFileSystemItem(userFileSystemOldPath).MoveAsync(userFileSystemNewPath);
                        LogMessage("Renamed succesefully:", userFileSystemOldPath, userFileSystemNewPath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"{e.ChangeType} failed:", $"From:{remoteStorageOldPath} To:{remoteStorageNewPath}", ex);
            }
        }

        private void Error(object sender, ErrorEventArgs e)
        {
            LogError(null, null, e.GetException());
        }

        protected void LogError(string message, string sourcePath, Exception ex)
        {
            string att = FsPath.Exists(sourcePath) ? FsPath.GetAttString(sourcePath) : null;
            log.Error($"\n{DateTime.Now} [{Thread.CurrentThread.ManagedThreadId,2}] {"Remote Storage Monitor: ",-26}{message,-45} {sourcePath,-80} {att} ", ex);
        }

        protected void LogMessage(string message, string sourcePath = null, string targetPath = null)
        {
            string att = FsPath.Exists(sourcePath) ? FsPath.GetAttString(sourcePath) : null;
            string size = FsPath.Size(sourcePath);

            log.Debug($"\n{DateTime.Now} [{Thread.CurrentThread.ManagedThreadId,2}] {"Remote Storage Monitor: ",-26}{message,-45} {sourcePath,-80} {size,7} {att} {targetPath}");
        }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    watcher.Dispose();
                    LogMessage($"Disposed");
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
