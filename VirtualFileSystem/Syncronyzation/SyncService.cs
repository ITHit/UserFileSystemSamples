using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Provider;

namespace VirtualFileSystem.Syncronyzation
{

    // 4194304  (0x400000)	FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS        (M)
    // 4096 	(0x1000) 	FILE_ATTRIBUTE_OFFLINE                      (O)
    // 1024 	(0x400)  	FILE_ATTRIBUTE_REPARSE_POINT                (L)
    // 16   	(0x10)   	FILE_ATTRIBUTE_DIRECTORY                    (D)
    //          (0x00080000)FILE_ATTRIBUTE_PINNED                       (P)
    //          (0x00100000)FILE_ATTRIBUTE_UNPINNED                     (U)

    [Flags]
    public enum FileAttributesExt
    {
        Pinned      = 0x00080000,
        Unpinned    = 0x00100000,
        Offline     = 0x1000
    }

    /// <summary>
    /// Doing full synchronization between client and server.
    /// </summary>
    /// <remarks>
    /// This is a simple full synchronyzation example. 
    /// </remarks>
    internal class SyncService : IDisposable
    {
        System.Timers.Timer timer = null;

        /// <summary>
        /// User file system path.
        /// </summary>
        private string userFileSystemRootPath;

        /// <summary>
        /// Logger.
        /// </summary>
        private ILog log;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="syncIntervalMs">Synchronization interval in milliseconds.</param>
        /// <param name="userFileSystemRootPath">User file system root path.</param>
        /// <param name="log">Logger.</param>
        internal SyncService(double syncIntervalMs, string userFileSystemRootPath, ILog log)
        {
            timer = new System.Timers.Timer(syncIntervalMs);
            this.userFileSystemRootPath = userFileSystemRootPath;
            this.log = log;
        }

        /// <summary>
        /// Starts synchronization.
        /// </summary>
        internal async Task StartAsync()
        {
            timer.Elapsed += Timer_ElapsedAsync;

            // Do not start next synchronyzation automatically, wait untill previous synchronyzation completed.
            timer.AutoReset = false; 
            timer.Start();

            LogMessage($"Started");
        }

        private async void Timer_ElapsedAsync(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                // Recursivery synchronize all moved files and folders present in the user file system on the client machine.
                await SyncronizeMovedAsync(userFileSystemRootPath);

                // Recursivery synchronize all updated/deleted/created folders present in the user file system on the client machine.
                await SyncronizeFolderAsync(userFileSystemRootPath);

                // Wait and than start synchronyzation again.
                timer.Start();
            }
            catch(Exception ex)
            {
                LogError("Timer failure:", null, ex);
                throw;
            }
        }

        /// <summary>
        /// Recursively synchronizes all moved files and folders with the server. 
        /// Synchronizes only folders already loaded into the user file system.
        /// </summary>
        /// <param name="userFileSystemFolderPath">Folder path in user file system.</param>
        private async Task SyncronizeMovedAsync(string userFileSystemFolderPath)
        {
            // In case of on-demand loading the user file system contains only a subset of the server files and folders.
            // Here we sync folder only if its content already loaded into user file system (folder is not offline).
            // The folder content is loaded inside IFolder.GetChildrenAsync() method.
            if (new DirectoryInfo(userFileSystemFolderPath).Attributes.HasFlag(System.IO.FileAttributes.Offline))
            {
                //LogMessage("Folder offline, skipping:", userFileSystemFolderPath);
                return;
            }

            IEnumerable<string> userFileSystemChildren = Directory.EnumerateFileSystemEntries(userFileSystemFolderPath, "*");
            //LogMessage("Synchronizing:", userFileSystemFolderPath);

            foreach (string userFileSystemPath in userFileSystemChildren)
            {
                try
                {
                    string remoteStoragePath = Mapping.MapPath(userFileSystemPath);

                    if (!FsPath.AvoidSync(userFileSystemPath) && !FsPath.AvoidSync(remoteStoragePath)
                        && PlaceholderItem.IsPlaceholder(userFileSystemPath)
                        && PlaceholderItem.GetItem(userFileSystemPath).IsMoved())
                    {
                        // Process items moved in user file system.
                        string userFileSystemOldPath = PlaceholderItem.GetItem(userFileSystemPath).GetOriginalPath();
                        LogMessage("Ttem moved, updating:", userFileSystemOldPath, userFileSystemPath);
                        string remoteStorageOldPath = Mapping.MapPath(userFileSystemOldPath);
                        await new RemoteStorageItem(remoteStorageOldPath).MoveToAsync(userFileSystemPath);
                        LogMessage("Moved succesefully:", remoteStorageOldPath);
                    }
                }
                catch (Exception ex)
                {
                    LogError("Move failed:", userFileSystemPath, ex);
                }

                // Synchronize subfolders.
                try
                {
                    if (FsPath.IsFolder(userFileSystemPath))
                    {
                        await SyncronizeMovedAsync(userFileSystemPath);
                    }
                }
                catch (Exception ex)
                {
                    LogError("Folder sync failed:", userFileSystemPath, ex);
                }
            }
        }

        /// <summary>
        /// Recursively synchronizes all files and folders with the server. 
        /// Synchronizes only folders already loaded into the user file system.
        /// </summary>
        /// <param name="userFileSystemFolderPath">Folder path in user file system.</param>
        private async Task SyncronizeFolderAsync(string userFileSystemFolderPath)
        {
            // In case of on-demand loading the user file system contains only a subset of the server files and folders.
            // Here we sync folder only if its content already loaded into user file system (folder is not offline).
            // The folder content is loaded inside IFolder.GetChildrenAsync() method.
            if (new DirectoryInfo(userFileSystemFolderPath).Attributes.HasFlag(System.IO.FileAttributes.Offline))
            {
//                LogMessage("Folder offline, skipping:", userFileSystemFolderPath);
                return;
            }

            IEnumerable<string> userFileSystemChildren = Directory.EnumerateFileSystemEntries(userFileSystemFolderPath, "*");
//            LogMessage("Synchronizing:", userFileSystemFolderPath);

            string remoteStorageFolderPath = Mapping.MapPath(userFileSystemFolderPath);
            IEnumerable<FileSystemInfo> remoteStorageChildrenItems = new DirectoryInfo(remoteStorageFolderPath).EnumerateFileSystemInfos("*");

            /*
            // Delete files/folders in user file system.
            foreach (string userFileSystemPath in userFileSystemChildren)
            {
                try
                {
                    string remoteStoragePath = Mapping.MapPath(userFileSystemPath);
                    FileSystemInfo remoteStorageItem = remoteStorageChildrenItems.FirstOrDefault(x => x.FullName.Equals(remoteStoragePath, StringComparison.InvariantCultureIgnoreCase));
                    if (remoteStorageItem == null)
                    {
                        LogMessage("Deleting item:", userFileSystemPath);
                        await new UserFileSystemItem(userFileSystemPath).DeleteAsync();
                        LogMessage("Deleted succesefully:", userFileSystemPath);
                    }
                }
                catch (Exception ex)
                {
                    LogError("Delete failed:", userFileSystemPath, ex);
                }
            }
            */
            
            // Create new files/folders in user file system.
            foreach (FileSystemInfo remoteStorageItem in remoteStorageChildrenItems)
            {
                try
                {
                    if (!FsPath.AvoidSync(remoteStorageItem.FullName))
                    {
                        string userFileSystemPath = Mapping.ReverseMapPath(remoteStorageItem.FullName);
                        if (!FsPath.Exists(userFileSystemPath))
                        {
                            LogMessage("Creating new item:", userFileSystemPath);
                            await UserFileSystemItem.CreateAsync(userFileSystemFolderPath, remoteStorageItem);
                            LogMessage("Created succesefully:", userFileSystemPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError("Creation failed:", remoteStorageItem.FullName, ex);
                }
            }
            
            // Update files/folders in user file system and sync subfolders.
            userFileSystemChildren = Directory.EnumerateFileSystemEntries(userFileSystemFolderPath, "*");
            foreach (string userFileSystemPath in userFileSystemChildren)
            {
                try
                {
                    string remoteStoragePath = Mapping.MapPath(userFileSystemPath);
                    FileSystemInfo remoteStorageItem = remoteStorageChildrenItems.FirstOrDefault(x => x.FullName.Equals(remoteStoragePath, StringComparison.InvariantCultureIgnoreCase));

                    
                    // Convert regular file/folder to placeholder. The file/folder was created or overwritten on the client.
                    if (!PlaceholderItem.IsPlaceholder(userFileSystemPath))
                    {
                        PlaceholderItem.ConvertToPlaceholder(userFileSystemPath, false);
                        LogMessage("Converted to placeholder:", userFileSystemPath);
                    }
                    
                    if (!FsPath.AvoidSync(userFileSystemPath) && !FsPath.AvoidSync(remoteStoragePath))
                    {
                        if (remoteStorageItem == null)
                        {   
                            if (!PlaceholderItem.GetItem(userFileSystemPath).GetInSync())
                            {
                                // Create the file/folder in the remote storage.
                                LogMessage("Creating item:", remoteStoragePath);
                                await RemoteStorageItem.CreateAsync(remoteStoragePath, userFileSystemPath);
                                LogMessage("Created succesefully:", remoteStoragePath);
                            }
                            else
                            {
                                // Delete the file/folder in user file system.
                                LogMessage("Deleting item:", userFileSystemPath);
                                await new UserFileSystemItem(userFileSystemPath).DeleteAsync();
                                LogMessage("Deleted succesefully:", userFileSystemPath);
                            }                            
                        }
                        else
                        {
                            if (!PlaceholderItem.GetItem(userFileSystemPath).GetInSync())
                            {
                                // User file system -> remote storage update.
                                LogMessage("Item modified, updating:", userFileSystemPath);
                                await new RemoteStorageItem(remoteStoragePath).UpdateAsync(userFileSystemPath);
                                LogMessage("Updated succesefully:", remoteStoragePath);
                            }
                            else if (!await new UserFileSystemItem(userFileSystemPath).ETagEqualsAsync(remoteStorageItem))
                            {
                                // User file system <- remote storage update.
                                LogMessage("Item modified, updating:", remoteStoragePath);
                                await new UserFileSystemItem(userFileSystemPath).UpdateAsync(remoteStorageItem);
                                LogMessage("Updated succesefully:", userFileSystemPath);
                            }

                            // Hydrate / dehydrate the file.
                            if(new UserFileSystemItem(userFileSystemPath).HydrationRequired())
                            {
                                LogMessage("Hydrating:", userFileSystemPath);
                                new PlaceholderFile(userFileSystemPath).Hydrate(0, -1);
                                LogMessage("Hydrated succesefully:", userFileSystemPath);
                            }
                            else if (new UserFileSystemItem(userFileSystemPath).DehydrationRequired())
                            {
                                LogMessage("Dehydrating:", userFileSystemPath);
                                new PlaceholderFile(userFileSystemPath).Dehydrate(0, -1, false);
                                LogMessage("Dehydrated succesefully:", userFileSystemPath);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError("Update failed:", userFileSystemPath, ex);
                }

                // Synchronize subfolders.
                try
                {                    
                    if (Directory.Exists(userFileSystemPath))
                    {
                        await SyncronizeFolderAsync(userFileSystemPath);
                    }
                }
                catch (Exception ex)
                {
                    LogError("Folder sync failed:", userFileSystemPath, ex);
                }
            }
        }

        private void Error(object sender, ErrorEventArgs e)
        {
            LogError(null, null, e.GetException());
        }

        protected void LogError(string message, string sourcePath, Exception ex)
        {
            log.Error($"\n{DateTime.Now} [{Thread.CurrentThread.ManagedThreadId,2}] {"Sync Service: ",-26}{message,-45} {sourcePath,-80}", ex);
        }

        protected void LogMessage(string message, string sourcePath = null, string targetPath = null)
        {
            log.Debug($"\n{DateTime.Now} [{Thread.CurrentThread.ManagedThreadId,2}] {"Sync Service: ",-26}{message,-45} {sourcePath,-80} {targetPath}");
        }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    timer.Stop();
                    timer.Dispose();
                    LogMessage($"Disposed");
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~SyncService()
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
