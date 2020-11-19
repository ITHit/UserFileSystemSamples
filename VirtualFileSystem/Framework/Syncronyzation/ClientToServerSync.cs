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
    /// <summary>
    /// User File System to Remote Storage synchronization.
    /// </summary>
    /// <remarks>In most cases you can use this class in your project without any changes.</remarks>
    internal class ClientToServerSync : Logger
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="log">Logger.</param>
        internal ClientToServerSync(ILog log) : base("UFS -> RS Sync", log)
        {

        }

        /// <summary>
        /// Recursively synchronizes all files and folders moved in user file system with the remote storage. 
        /// Synchronizes only folders already loaded into the user file system.
        /// </summary>
        /// <param name="userFileSystemFolderPath">Folder path in user file system.</param>
        internal async Task SyncronizeMovedAsync(string userFileSystemFolderPath)
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
                string userFileSystemOldPath = null;
                try
                {
                    //$<PlaceholderItem.IsPlaceholder
                    if (!PlaceholderItem.IsPlaceholder(userFileSystemPath))
                    {
                        // Convert regular file/folder to placeholder. 
                        // The file/folder was created or overwritten.
                        PlaceholderItem.ConvertToPlaceholder(userFileSystemPath, false);
                        LogMessage("Converted to placeholder", userFileSystemPath);
                    }
                    //$>

                    if (!FsPath.AvoidSync(userFileSystemPath))
                    {
                        PlaceholderItem userFileSystemItem = PlaceholderItem.GetItem(userFileSystemPath);
                        if (userFileSystemItem.IsMoved())
                        {
                            // Process items moved in user file system.                            
                            userFileSystemOldPath = userFileSystemItem.GetOriginalPath();
                            await new RemoteStorageRawItem(userFileSystemOldPath, this).MoveToAsync(userFileSystemPath);
                        }
                        else
                        {
                            // Restore Original Path and 'locked' icon that are lost during MS Office transactional save.
                            // We keep Original Path to process moved files when app was not running.      
                            userFileSystemOldPath = userFileSystemItem.GetOriginalPath();
                            if (!userFileSystemItem.IsNew() && string.IsNullOrEmpty(userFileSystemOldPath))
                            {
                                // Restore Original Path.
                                LogMessage("Saving Original Path", userFileSystemItem.Path);
                                userFileSystemItem.SetOriginalPath(userFileSystemItem.Path);

                                // Restore the 'locked' icon.
                                bool isLocked = await Lock.IsLockedAsync(userFileSystemPath);
                                await new UserFileSystemRawItem(userFileSystemPath).SetLockIconAsync(isLocked);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError("Move in remote storage failed", userFileSystemOldPath, userFileSystemPath, ex);
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
                    LogError("Folder move sync failed", userFileSystemPath, null, ex);
                }
            }
        }

        /// <summary>
        /// Recursively updates and creates files and folders in remote storage. 
        /// Synchronizes only folders already loaded into the user file system.
        /// </summary>
        /// <param name="userFileSystemFolderPath">Folder path in user file system.</param>
        internal async Task SyncronizeFolderAsync(string userFileSystemFolderPath)
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


            // Update and create files/folders in remote storage.
            userFileSystemChildren = Directory.EnumerateFileSystemEntries(userFileSystemFolderPath, "*");
            foreach (string userFileSystemPath in userFileSystemChildren)
            {
                try
                {
                    if (!FsPath.AvoidSync(userFileSystemPath))
                    {
                        if (PlaceholderItem.GetItem(userFileSystemPath).IsNew())
                        {
                            // Create a file/folder in the remote storage.
                            await RemoteStorageRawItem.CreateAsync(userFileSystemPath, this);
                        }
                        else
                        {
                            // Update file/folder in the remote storage. Unlock if auto-locked.
                            await new RemoteStorageRawItem(userFileSystemPath, this).UpdateAsync();
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
