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
    /// Monitors files and folders creation as well as attributes change in the user file system.
    /// If any file or folder pinned or unpinned, triggers hydration or dehydration.
    /// </summary>
    /// <remarks>
    /// Windows does not provide any notifications for pinned/unpinned attributes change as well as for files/folders creation. 
    /// We need to monitor them using regular FileSystemWatcher.
    /// 
    /// In most cases you can use this class in your project without any changes.
    /// </remarks>
    internal class UserFileSystemMonitor : Logger, IDisposable
    {
        /// <summary>
        /// User file system watcher.
        /// </summary>
        private FileSystemWatcher watcher = new FileSystemWatcher();

        /// <summary>
        /// User file system root path. Folder to minitor changes in.
        /// </summary>
        private string userFileSystemRootPath;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemRootPath">User file system root path. Attributes are monitored in this folder.</param>
        /// <param name="log">Logger.</param>
        internal UserFileSystemMonitor(string userFileSystemRootPath, ILog log) : base("User File System Monitor", log)
        {
            this.userFileSystemRootPath = userFileSystemRootPath;
        }

        /// <summary>
        /// Starts monitoring attributes changes in user file system.
        /// </summary>
        internal async Task StartAsync()
        {
            watcher.IncludeSubdirectories = true;
            watcher.Path = userFileSystemRootPath;
            //watcher.Filter = "*.*";
            watcher.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.Error += Error;
            watcher.Created += CreatedAsync;
            watcher.Changed += ChangedAsync;
            watcher.Deleted += DeletedAsync;
            watcher.Renamed += RenamedAsync;
            watcher.EnableRaisingEvents = true;
            
            LogMessage($"Started");
        }


        /// <summary>
        /// Called when a file or folder is created in the user file system.
        /// </summary>
        /// <remarks>
        /// This method is also called when a file is being moved in user file system, after the IFileSystem.MoveToAsync() call.
        /// </remarks>
        private async void CreatedAsync(object sender, FileSystemEventArgs e)
        {
            LogMessage($"{e.ChangeType}", e.FullPath);
            string userFileSystemPath = e.FullPath;
            try
            {

                // When a file/folder is moved this method is also called. The file/folder move is already processed in IFileSystem.MoveToAsync().
                if (FsPath.Exists(userFileSystemPath) && !PlaceholderItem.IsPlaceholder(userFileSystemPath))
                {
                    // When a new file or folder is created under sync root it is 
                    // created as a regular file or folder. Converting to placeholder.
                    PlaceholderItem.ConvertToPlaceholder(userFileSystemPath, false);
                    LogMessage("Converted to placeholder", userFileSystemPath);

                    // Do not create temp MS Office, temp and hidden files in remote storage. 
                    if (!FsPath.AvoidSync(userFileSystemPath))
                    {
                        // Create the file/folder in the remote storage.
                        try
                        {
                            await RemoteStorageRawItem.CreateAsync(userFileSystemPath, this);
                        }
                        catch (IOException ex)
                        {
                            LogError("Creation in remote storage failed. Possibly in use by an application", userFileSystemPath, null, ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"{e.ChangeType} failed", userFileSystemPath, null, ex);
            }
        }

        /// <summary>
        /// Called when a file or folder attributes changed in the user file system.
        /// </summary>
        /// <remarks>
        /// Here we monitor pinned and unpinned attributes and hydrate/dehydrate files.
        /// </remarks>
        private async void ChangedAsync(object sender, FileSystemEventArgs e)
        {
            LogMessage($"{e.ChangeType}", e.FullPath);
            try
            {
                string userFileSystemPath = e.FullPath;
                if (FsPath.Exists(userFileSystemPath) && !FsPath.AvoidSync(userFileSystemPath))
                {
                    // Hydrate / dehydrate.
                    if (new UserFileSystemRawItem(userFileSystemPath).HydrationRequired())
                    {
                        LogMessage("Hydrating", userFileSystemPath);
                        new PlaceholderFile(userFileSystemPath).Hydrate(0, -1);
                        LogMessage("Hydrated succesefully", userFileSystemPath);
                    }
                    else if (new UserFileSystemRawItem(userFileSystemPath).DehydrationRequired())
                    {
                        LogMessage("Dehydrating", userFileSystemPath);
                        new PlaceholderFile(userFileSystemPath).Dehydrate(0, -1);
                        LogMessage("Dehydrated succesefully", userFileSystemPath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Hydration/dehydration failed", e.FullPath, null, ex);
            }
        }

        /// <summary>
        /// Called when a file or folder is deleted in the user file system.
        /// </summary>
        /// <remarks>We monitor this event for logging purposes only.</remarks>
        private async void DeletedAsync(object sender, FileSystemEventArgs e)
        {
            LogMessage(e.ChangeType.ToString(), e.FullPath);
        }

        /// <summary>
        /// Called when a file or folder is renamed in the user file system.
        /// </summary>
        /// <remarks>We monitor this event for logging purposes only.</remarks>
        private async void RenamedAsync(object sender, RenamedEventArgs e)
        {
            LogMessage("Renamed", e.OldFullPath, e.FullPath);
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
