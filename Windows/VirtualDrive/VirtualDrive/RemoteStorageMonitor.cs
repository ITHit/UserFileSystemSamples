using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

using log4net;
using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Samples.Common;

namespace VirtualDrive
{
    /// <summary>
    /// Monitors changes in the remote storage, notifies the client and updates the user file system.
    /// If any file or folder is is modified, created, delated, renamed or attributes changed in the remote storage, 
    /// triggers an event with information about changes.
    /// </summary>
    /// <remarks>
    /// Here, for demo purposes we simulate server by monitoring source file path using FileWatchWrapper.
    /// In your application, instead of using FileWatchWrapper, you will connect to your remote storage using web sockets 
    /// or use any other technology to get notifications about changes in your remote storage.
    /// </remarks>
    public class RemoteStorageMonitor : Logger, IDisposable
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
        /// Virtul drive instance. This class will call <see cref="Engine"/> methods 
        /// to update user file system when any data is changed in the remote storage.
        /// </summary>
        private readonly VirtualEngine engine;

        /// <summary>
        /// Watches for changes in the remote storage file system.
        /// </summary>
        private readonly FileSystemWatcherQueued watcher = new FileSystemWatcherQueued();

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="remoteStorageRootPath">Remote storage path. Folder that contains source files to monitor changes.</param>
        /// <param name="engine">Virtual drive to send notifications about changes in the remote storage.</param>
        /// <param name="log">Logger.</param>
        internal RemoteStorageMonitor(string remoteStorageRootPath, VirtualEngine engine, ILog log) : base("Remote Storage Monitor", log)
        {
            this.engine = engine;

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
        internal void Start()
        {
            watcher.EnableRaisingEvents = true;
            LogMessage($"Started", watcher.Path);
        }

        /// <summary>
        /// Stops monitoring changes in the remote storage.
        /// </summary>
        internal void Stop()
        {
            watcher.EnableRaisingEvents = false;
            LogMessage($"Stopped", watcher.Path);
        }

        /// <summary>
        /// Called when a file or folder is created in the remote storage.
        /// </summary>
        /// <remarks>In this method we create a new file/folder in the user file system.</remarks>
        private async void CreatedAsync(object sender, FileSystemEventArgs e)
        {
            LogMessage(e.ChangeType.ToString(), e.FullPath);
            string remoteStoragePath = e.FullPath;
            try
            {
                string userFileSystemPath = Mapping.ReverseMapPath(remoteStoragePath);

                // This check is only required because we can not prevent circular calls because of the simplicity of this example.
                // In your real-life application you will not send updates from server back to client that issued the update.
                if (!FsPath.Exists(userFileSystemPath))
                {
                    string userFileSystemParentPath = Path.GetDirectoryName(userFileSystemPath);

                    // Because of the on-demand population the file or folder placeholder may not exist in the user file system
                    // or the folder may be offline.
                    FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(remoteStoragePath);
                    if (remoteStorageItem != null)
                    {
                        IFileSystemItemMetadata newItemInfo = Mapping.GetUserFileSysteItemMetadata(remoteStorageItem);
                        if (await engine.ServerNotifications(userFileSystemParentPath).CreateAsync(new[] { newItemInfo }) > 0)
                        {
                            LogMessage($"Created succesefully", userFileSystemPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"{e.ChangeType} failed", remoteStoragePath, null, ex);
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
            string userFileSystemPath = null;
            try
            {
                userFileSystemPath = Mapping.ReverseMapPath(remoteStoragePath);

                // This check is only required because we can not prevent circular calls because of the simplicity of this example.
                // In your real-life application you will not send updates from server back to client that issued the update.
                if (Mapping.IsModified(userFileSystemPath, remoteStoragePath))
                {
                    FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(remoteStoragePath);
                    IFileSystemItemMetadata itemInfo = Mapping.GetUserFileSysteItemMetadata(remoteStorageItem);

                    // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                    if (await engine.ServerNotifications(userFileSystemPath).UpdateAsync(itemInfo))
                    {
                        // Update ETag.
                        //await engine.Mapping.UpdateETagAsync(remoteStoragePath, userFileSystemPath);

                        LogMessage("Updated succesefully", userFileSystemPath);
                    }
                }
            }
            catch (IOException ex)
            {
                // The file is blocked in the user file system. This is a normal behaviour.
                LogMessage(ex.Message);
            }
            catch (Exception ex)
            {
                LogError($"{e.ChangeType} failed", remoteStoragePath, null, ex);
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

                // This check is only required because we can not prevent circular calls because of the simplicity of this example.
                // In your real-life application you will not send updates from server back to client that issued the update.
                if (FsPath.Exists(userFileSystemPath))
                {
                    // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                    if (await engine.ServerNotifications(userFileSystemPath).DeleteAsync())
                    {
                        LogMessage("Deleted succesefully", userFileSystemPath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"{e.ChangeType} failed", remoteStoragePath, null, ex);
            }
        }

        /// <summary>
        /// Called when a file or folder is renamed in the remote storage.
        /// </summary>
        /// <remarks>In this method we rename corresponding file/folder in user file system.</remarks>
        private async void RenamedAsync(object sender, RenamedEventArgs e)
        {
            LogMessage("Renamed", e.OldFullPath, e.FullPath);
            string remoteStorageOldPath = e.OldFullPath;
            string remoteStorageNewPath = e.FullPath;
            try 
            {
                string userFileSystemOldPath = Mapping.ReverseMapPath(remoteStorageOldPath);
                string userFileSystemNewPath = Mapping.ReverseMapPath(remoteStorageNewPath);

                // This check is only required because we can not prevent circular calls because of the simplicity of this example.
                // In your real-life application you will not send updates from server back to client that issued the update.
                if (FsPath.Exists(userFileSystemOldPath))
                {
                    // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                    if (await engine.ServerNotifications(userFileSystemOldPath).MoveToAsync(userFileSystemNewPath))
                    {
                        LogMessage("Renamed succesefully", userFileSystemOldPath, userFileSystemNewPath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"{e.ChangeType} failed", remoteStorageOldPath, remoteStorageNewPath, ex);
            }
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
