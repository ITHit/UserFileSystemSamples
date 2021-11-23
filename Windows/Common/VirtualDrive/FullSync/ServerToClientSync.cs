using log4net;
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

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Synchronizes files and folders from remote storage to user file system.
    /// </summary>
    /// <remarks>In most cases you can use this class in your project without any changes.</remarks>
    internal class ServerToClientSync : Logger
    {
        /// <summary>
        /// Virtual drive.
        /// </summary>
        private readonly VirtualEngineBase engine;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        /// <param name="log">Logger.</param>
        internal ServerToClientSync(VirtualEngineBase engine, ILog log) : base("UFS <- RS Sync", log)
        {
            this.engine = engine;
        }

        /// <summary>
        /// Recursively synchronizes all files and folders from server to client. 
        /// Synchronizes only folders already loaded into the user file system.
        /// </summary>
        /// <param name="userFileSystemFolderPath">Folder path in user file system.</param>
        internal async Task SyncronizeFolderAsync(string userFileSystemFolderPath)
        {
            // In case of on-demand loading the user file system contains only a subset of the server files and folders.
            // Here we sync folder only if its content already loaded into user file system (folder is not offline).
            // We also skip regular folders (that are not placeholders), for example new folders.
            FileAttributes folderAttributes = new DirectoryInfo(userFileSystemFolderPath).Attributes;
            if (   (folderAttributes & FileAttributes.Offline) != 0
                || (folderAttributes & FileAttributes.ReparsePoint) == 0
                )
            {
                // LogMessage("Folder is offline or is new, skipping:", userFileSystemFolderPath);
                return;
            }

            IEnumerable<string> userFileSystemChildren = Directory.EnumerateFileSystemEntries(userFileSystemFolderPath, "*");
            //LogMessage("Synchronizing:", userFileSystemFolderPath);

            IVirtualFolder userFolder = await engine.GetFileSystemItemAsync(userFileSystemFolderPath, FileSystemItemType.Folder, null) as IVirtualFolder;
            IEnumerable<FileSystemItemMetadataExt> remoteStorageChildrenItems = await userFolder.EnumerateChildrenAsync("*");
            //string remoteStorageFolderPath = Mapping.MapPath(userFileSystemFolderPath);

            // Create new files/folders in the user file system.
            foreach (FileSystemItemMetadataExt remoteStorageItem in remoteStorageChildrenItems)
            {
                string userFileSystemPath = Path.Combine(userFileSystemFolderPath, remoteStorageItem.Name);
                //string remoteStoragePath = Path.Combine(remoteStorageFolderPath, remoteStorageItem.Name);
                try
                {
                    // We do not want to sync MS Office temp files, etc. from the remote storage.
                    // We also do not want to create MS Office files during transactional save in the user file system.
                    if (   !FilterHelper.AvoidSync(userFileSystemPath) && !FilterHelper.IsAppLocked(userFileSystemPath)
                        /*&& !FilterHelper.AvoidSync(remoteStoragePath)  && !FilterHelper.IsAppLocked(remoteStoragePath)*/)
                    {
                        if (!FsPath.Exists(userFileSystemPath))
                        {
                            LogMessage("Creating", userFileSystemPath);
                            await engine.ServerNotifications(userFileSystemFolderPath).CreateAsync(new[] { remoteStorageItem });

                            await engine.ExternalDataManager(userFileSystemPath).SetCustomDataAsync(
                                remoteStorageItem.ETag, 
                                remoteStorageItem.IsLocked, 
                                remoteStorageItem.CustomProperties);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError("Creation failed", userFileSystemPath, null, ex);
                }
            }
            
            // Delete and update files/folders in user file system and sync subfolders.
            userFileSystemChildren = Directory.EnumerateFileSystemEntries(userFileSystemFolderPath, "*");
            foreach (string userFileSystemPath in userFileSystemChildren)
            {
                try
                {
                    string itemName = Path.GetFileName(userFileSystemPath);
                    FileSystemItemMetadataExt remoteStorageItem = remoteStorageChildrenItems.FirstOrDefault(x => x.Name.Equals(itemName, StringComparison.InvariantCultureIgnoreCase));
                    
                    if (!FilterHelper.AvoidSync(userFileSystemPath)
                        && PlaceholderItem.IsPlaceholder(userFileSystemPath))
                    {
                        if (remoteStorageItem == null)
                        {   
                            // We do not want to delete items created or modified on the client.
                            //if (PlaceholderItem.GetItem(userFileSystemPath).GetInSync()
                            //    && !engine.ExternalDataManager(userFileSystemPath, this).IsNew)
                            {
                                // Delete the file/folder in user file system.
                                LogMessage("Deleting", userFileSystemPath);
                                await engine.ServerNotifications(userFileSystemPath).DeleteAsync();
                                engine.ExternalDataManager(userFileSystemPath, this).Delete();
                            }                            
                        }
                        else
                        {
                            //string remoteStoragePath = Path.Combine(remoteStorageFolderPath, remoteStorageItem.Name);
                            if (PlaceholderItem.GetItem(userFileSystemPath).GetInSync()                                 // user file system item is NOT modified
                                && await engine.Mapping.IsModifiedAsync(userFileSystemPath, remoteStorageItem, this))   // remote storage item IS modified
                            {
                                // User file system <- remote storage update.
                                LogMessage("Updating", userFileSystemPath);
                                await engine.ServerNotifications(userFileSystemPath).UpdateAsync(remoteStorageItem);
                            //}

                            // Set custom columns data.
                            //if (PlaceholderItem.GetItem(userFileSystemPath).GetInSync())
                            //{
                                await engine.ExternalDataManager(userFileSystemPath).SetCustomDataAsync(
                                    remoteStorageItem.ETag, 
                                    remoteStorageItem.IsLocked, 
                                    remoteStorageItem.CustomProperties);
                            }

                            // Hydrate / dehydrate the file.
                            if (FsPath.IsFile(userFileSystemPath))
                            {
                                PlaceholderFile userFileSystemItem = PlaceholderItem.GetItem(userFileSystemPath) as PlaceholderFile;
                                if (userFileSystemItem.HydrationRequired())
                                {
                                    LogMessage("Hydrating", userFileSystemPath);
                                    try
                                    {
                                        userFileSystemItem.Hydrate(0, -1);
                                    }
                                    catch (FileLoadException)
                                    {
                                        // Typically this happens if another thread already hydrating the file.
                                        LogMessage("Failed to hydrate. The file is blocked.", userFileSystemPath);
                                    }
                                }
                                else if (userFileSystemItem.DehydrationRequired())
                                {
                                    LogMessage("Dehydrating", userFileSystemPath);
                                    new PlaceholderFile(userFileSystemPath).Dehydrate(0, -1);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError("Update failed", userFileSystemPath, null, ex);
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
                    LogError("Folder sync failed:", userFileSystemPath, null, ex);
                }
            }
        }


    }
}
