using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using System.Linq;

namespace VirtualFileSystem
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
    internal class RemoteStorageMonitor : Logger, IDisposable
    {
        /// <summary>
        /// Remote storage path. Folder to monitor for changes.
        /// </summary>
        private readonly string remoteStorageRootPath;

        /// <summary>
        /// Engine instance. We will call <see cref="Engine"/> methods 
        /// to update user file system when any data is changed in the remote storage.
        /// </summary>
        private readonly Engine engine;

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
        internal RemoteStorageMonitor(string remoteStorageRootPath, Engine engine, ILog log) : base("Remote Storage Monitor", log)
        {
            this.remoteStorageRootPath = remoteStorageRootPath;
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
            LogMessage($"Started");
        }

        /// <summary>
        /// Stops monitoring changes in the remote storage.
        /// </summary>
        internal void Stop()
        {
            watcher.EnableRaisingEvents = false;
            LogMessage($"Stopped");
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

                    FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(remoteStoragePath);
                    if (remoteStorageItem != null)
                    {
                        IFileSystemItemMetadata newItemInfo = Mapping.GetUserFileSysteItemMetadata(remoteStorageItem);
                        if (await engine.ServerNotifications(userFileSystemParentPath).CreateAsync(new[] { newItemInfo }) > 0)
                        {
                            // Because of the on-demand population, the parent folder placeholder may not exist in the user file system
                            // or the folder may be offline. In this case the IServerNotifications.CreateAsync() call is ignored.
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
                if (IsModified(userFileSystemPath, remoteStoragePath))
                {
                    FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(remoteStoragePath);
                    IFileSystemItemMetadata itemInfo = Mapping.GetUserFileSysteItemMetadata(remoteStorageItem);

                    if (await engine.ServerNotifications(userFileSystemPath).UpdateAsync(itemInfo))
                    {
                        // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                        // In this case the IServerNotifications.UpdateAsync() call is ignored.
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
                Thread.Sleep(2000); // This can be removed in a real-life application.
                if (FsPath.Exists(userFileSystemPath))
                {
                    if (await engine.ServerNotifications(userFileSystemPath).DeleteAsync())
                    {
                        // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                        // In this case the IServerNotifications.DeleteAsync() call is ignored.
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
                string userFileSystemOldPath = Mapping.ReverseMapPath(remoteStorageOldPath);
                string userFileSystemNewPath = Mapping.ReverseMapPath(remoteStorageNewPath);

                // This check is only required because we can not prevent circular calls because of the simplicity of this example.
                // In your real-life application you will not send updates from server back to client that issued the update.
                Thread.Sleep(2000); // This can be removed in a real-life application.
                if (FsPath.Exists(userFileSystemOldPath))
                {
                    if (await engine.ServerNotifications(userFileSystemOldPath).MoveToAsync(userFileSystemNewPath))
                    {
                        // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                        // In this case the IServerNotifications.MoveToAsync() call is ignored.
                        LogMessage("Renamed succesefully:", userFileSystemOldPath, userFileSystemNewPath);
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

        /// <summary>
        /// Compares two files contents.
        /// </summary>
        /// <param name="filePath1">File or folder 1 to compare.</param>
        /// <param name="filePath2">File or folder 2 to compare.</param>
        /// <returns>True if file is modified. False - otherwise.</returns>
        private static bool IsModified(string filePath1, string filePath2)
        {
            if(FsPath.IsFolder(filePath1) && FsPath.IsFolder(filePath2))
            {
                return false;
            }

            try
            {
                if (new FileInfo(filePath1).Length == new FileInfo(filePath2).Length)
                {
                    // Verify that the file is not offline,
                    // therwise the file will be hydrated when the file stream is opened.
                    if (new FileInfo(filePath1).Attributes.HasFlag(System.IO.FileAttributes.Offline)
                        || new FileInfo(filePath1).Attributes.HasFlag(System.IO.FileAttributes.Offline))
                    {
                        return false;
                    }

                    byte[] hash1;
                    byte[] hash2;
                    using (var alg = System.Security.Cryptography.MD5.Create())
                    {
                        using (FileStream stream = new FileStream(filePath1, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            hash1 = alg.ComputeHash(stream);
                        }
                        using (FileStream stream = new FileStream(filePath2, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            hash2 = alg.ComputeHash(stream);
                        }
                    }

                    return !hash1.SequenceEqual(hash2);
                }
            }
            catch(IOException)
            {
                // One of the files is blocked. Can not compare files and start sychronization.
                return false;
            }

            return true;
        }
    }
}
