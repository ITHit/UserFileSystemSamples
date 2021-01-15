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
    internal class RemoteStorageMonitor : Logger, IDisposable
    {

        /// <summary>
        /// Remote storage path. Folder to minitor changes in.
        /// </summary>
        private string remoteStorageRootPath;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="remoteStorageRootPath">Remote storage path. Folder that contains source files to monitor changes.</param>
        /// <param name="log">Logger.</param>
        internal RemoteStorageMonitor(string remoteStorageRootPath, ILog log) : base("Remote Storage Monitor", log)
        {
            this.remoteStorageRootPath = remoteStorageRootPath;
        }

        /// <summary>
        /// Starts monitoring changes on the server.
        /// </summary>
        internal async Task StartAsync()
        {

            
            //LogMessage($"Started");
        }
        /*
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
                // We do not want to sync MS Office temp files from remote storage.
                if (!FsPath.AvoidSync(remoteStoragePath))
                {
                    string userFileSystemPath = Mapping.ReverseMapPath(remoteStoragePath);
                    string userFileSystemParentPath = Path.GetDirectoryName(userFileSystemPath);

                    // Because of the on-demand population the file or folder placeholder may not exist in the user file system
                    // or the folder may be offline.
                    if (Directory.Exists(userFileSystemParentPath)
                        && !new DirectoryInfo(userFileSystemParentPath).Attributes.HasFlag(System.IO.FileAttributes.Offline))
                    {
                        FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(remoteStoragePath);
                        FileSystemItemBasicInfo newItemInfo = Mapping.GetUserFileSysteItemBasicInfo(remoteStorageItem);
                        await UserFileSystemRawItem.CreateAsync(userFileSystemParentPath, new[] { newItemInfo });
                        LogMessage($"Created succesefully", userFileSystemPath);
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
            try
            {
                // We do not want to sync MS Office temp files, etc. from remote storage.
                if (!FsPath.AvoidSync(remoteStoragePath))
                {
                    string userFileSystemPath = Mapping.ReverseMapPath(remoteStoragePath);

                    // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                    if (FsPath.Exists(userFileSystemPath))
                    {
                        FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(remoteStoragePath);

                        // This check is only required because we can not prevent circular calls because of the simplicity of this example.
                        // In your real-life application you will not sent updates from server back to client that issued the update.
                        FileSystemItemBasicInfo itemInfo = Mapping.GetUserFileSysteItemBasicInfo(remoteStorageItem);
                        if (!await ETag.ETagEqualsAsync(userFileSystemPath, itemInfo))
                        {
                            await new UserFileSystemRawItem(userFileSystemPath).UpdateAsync(itemInfo);
                            LogMessage("Updated succesefully", userFileSystemPath);
                        }

                        // Update "locked by another user" icon.
                        await new UserFileSystemRawItem(userFileSystemPath).SetLockedByAnotherUserAsync(itemInfo.LockedByAnotherUser);
                    }
                }
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
                if (!FsPath.AvoidSync(remoteStoragePath))
                {
                    string userFileSystemPath = Mapping.ReverseMapPath(remoteStoragePath);

                    // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                    if (FsPath.Exists(userFileSystemPath))
                    {
                        await new UserFileSystemRawItem(userFileSystemPath).DeleteAsync();
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
            LogMessage("Renamed:", e.OldFullPath, e.FullPath);
            string remoteStorageOldPath = e.OldFullPath;
            string remoteStorageNewPath = e.FullPath;
            try 
            {
                if (!FsPath.AvoidSync(remoteStorageOldPath) && !FsPath.AvoidSync(remoteStorageNewPath))
                {
                    string userFileSystemOldPath = Mapping.ReverseMapPath(remoteStorageOldPath);

                    // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                    if (FsPath.Exists(userFileSystemOldPath))
                    {
                        string userFileSystemNewPath = Mapping.ReverseMapPath(remoteStorageNewPath);
                        await new UserFileSystemRawItem(userFileSystemOldPath).MoveToAsync(userFileSystemNewPath);
                        LogMessage("Renamed succesefully:", userFileSystemOldPath, userFileSystemNewPath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"{e.ChangeType} failed", remoteStorageOldPath, remoteStorageNewPath, ex);
            }
        }
        */
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
                    
                    //LogMessage($"Disposed");
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
