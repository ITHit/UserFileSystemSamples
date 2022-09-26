using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;

namespace WebDAVDrive
{
    /// <summary>
    /// Synchronizes items from the remote storage to the user file system.
    /// </summary>
    /// <remarks>
    /// In case any updates from remote storage to user file system did not reach the 
    /// client (in case the client or network was offline or the item was blocked),
    /// this service synchronizes all created, updated and deleted items.
    /// </remarks>
    public class IncomingFullSync : IncomingServerNotifications
    {
        /// <summary>
        /// Synchronization interval in milliseconds. Default is 60000 ms.
        /// </summary>
        public double SyncIntervalMs = 60000;

        /// <summary>
        /// Last synchronization time.
        /// </summary>
        private DateTimeOffset lastRun = DateTimeOffset.MinValue;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        internal IncomingFullSync(VirtualEngine engine) 
            : base(engine, engine.Logger.CreateLogger("Incoming Full Sync"))
        {

        }

        internal async Task<bool> TrySyncronizeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Because full synchcronization may send alot of requests to the remote storage,
                // we want synchronization to run only after the SyncIntervalMs elapsed.
                if (lastRun <= DateTimeOffset.Now.AddMilliseconds(-SyncIntervalMs))
                {
                    lastRun = DateTimeOffset.MaxValue; // Wait until synchronization finished.

                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    string userFileSystemFolderPath = Engine.Path;
                    Logger.LogDebug("Synchronizing...", userFileSystemFolderPath);
                    await SyncronizeFolderAsync(userFileSystemFolderPath, cancellationToken);
                    lastRun = DateTimeOffset.Now;
                    watch.Stop();
                    string elapsed = watch.Elapsed.ToString(@"hh\:mm\:ss\.ff");
                    Logger.LogDebug($"Synchronization completed in {elapsed}", userFileSystemFolderPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message, Engine.Path, default, ex);
            }

            return false;
        }

        /// <summary>
        /// Recursively synchronizes all files and folders from the remote storage to the user file system. 
        /// Synchronizes only folders already loaded into the user file system.
        /// </summary>
        /// <param name="userFileSystemFolderPath">Folder path in the user file system.</param>
        private async Task SyncronizeFolderAsync(string userFileSystemFolderPath, CancellationToken cancellationToken = default)
        {
            // In case of on-demand loading the user file system contains only a subset of the server files and folders.
            // Here we sync folder only if its content already loaded into user file system (folder is not offline).
            // We also skip regular folders (that are not placeholders), for example new folders.
            FileAttributes folderAttributes = new DirectoryInfo(userFileSystemFolderPath).Attributes;
            if (   (folderAttributes & FileAttributes.Offline) != 0
                || (folderAttributes & FileAttributes.ReparsePoint) == 0
                )
            {
                // Skipping offline or new folder.
                return;
            }

            IEnumerable<string> userFileSystemChildren = Directory.EnumerateFileSystemEntries(userFileSystemFolderPath, "*");

            VirtualFolder userFolder = await Engine.GetFileSystemItemAsync(userFileSystemFolderPath, FileSystemItemType.Folder, null, Logger) as VirtualFolder;
            IEnumerable<FileSystemItemMetadataExt> remoteStorageChildrenItems = await userFolder.EnumerateChildrenAsync("*", cancellationToken);

            // Create new files/folders in the user file system.
            foreach (FileSystemItemMetadataExt remoteStorageItem in remoteStorageChildrenItems)
            {
                if (cancellationToken.IsCancellationRequested) return;

                string userFileSystemPath = Path.Combine(userFileSystemFolderPath, remoteStorageItem.Name);
                try
                {
                    if (!FsPath.Exists(userFileSystemPath))
                    {
                        await IncomingCreatedAsync(userFileSystemFolderPath, remoteStorageItem);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Creation failed", userFileSystemPath, null, ex);
                }
            }
            
            // Delete and update items in user file system and sync subfolders.
            userFileSystemChildren = Directory.EnumerateFileSystemEntries(userFileSystemFolderPath, "*");
            foreach (string userFileSystemPath in userFileSystemChildren)
            {
                if (cancellationToken.IsCancellationRequested) return;

                try
                {
                    string itemName = Path.GetFileName(userFileSystemPath);
                    FileSystemItemMetadataExt remoteStorageItem = remoteStorageChildrenItems.FirstOrDefault(x => x.Name.Equals(itemName, StringComparison.InvariantCultureIgnoreCase));
                    
                    // Check if this is a placeholder to avoid updating/deleting new items.
                    if (PlaceholderItem.IsPlaceholder(userFileSystemPath))
                    {
                        if (remoteStorageItem == null)
                        {
                            // The item is deleted in remote storage.

                            //if (PlaceholderItem.GetItem(userFileSystemPath).GetInSync())
                            {
                                // Delete the file/folder in user file system.
                                await IncomingDeletedAsync(userFileSystemPath);
                            }
                        }
                        else
                        {
                            PlaceholderItem item = Engine.Placeholders.GetItem(userFileSystemPath);
                            if (//item.GetInSync() &&                            // User file system item is NOT modified. This check is only to reduce number of requests to the remote storage.
                                await item.IsModifiedAsync(remoteStorageItem))   // Remote storage item IS modified.
                            {
                                // The item content is updated in the remote storage.

                                // User file system <- remote storage update.
                                await IncomingChangedAsync(userFileSystemPath, remoteStorageItem);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Update failed", userFileSystemPath, null, ex);
                }

                // Synchronize subfolders.
                try
                {                    
                    if (Directory.Exists(userFileSystemPath))
                    {
                        await SyncronizeFolderAsync(userFileSystemPath, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Folder sync failed", userFileSystemPath, null, ex);
                }
            }
        }

        /// <summary>
        /// Starts service.
        /// </summary>
        public async Task StartAsync()
        {
            Logger.LogMessage("Enabled");
        }

        /// <summary>
        /// Stops service.
        /// </summary>
        public async Task StopAsync()
        {
            lastRun = DateTimeOffset.MinValue;
            Logger.LogMessage("Disabled");
        }
    }
}
