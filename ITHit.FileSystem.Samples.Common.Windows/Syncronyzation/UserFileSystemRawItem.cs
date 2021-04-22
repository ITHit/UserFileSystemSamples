using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Provider;

namespace ITHit.FileSystem.Samples.Common.Windows.Syncronyzation
{
    /// <summary>
    /// Provides methods for synching the from remote storage to the user file system.
    /// Creates, updates and deletes placeholder files and folders based on the info from the remote storage.
    /// </summary>
    /// <remarks>In most cases you can use this class in your project without any changes.</remarks>
    internal class UserFileSystemRawItem : IServerNotifications
    {
        /// <summary>
        /// Path to the file or folder placeholder in user file system.
        /// </summary>
        private readonly string userFileSystemPath;

        /// <summary>
        /// Virtual drive instance.
        /// </summary>
        private readonly VirtualDriveBase virtualDrive;

        /// <summary>
        /// Logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// ETag manager.
        /// </summary>
        private readonly ETagManager eTagManager;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">File or folder path in user file system.</param>
        /// <param name="logger">Logger.</param>
        public UserFileSystemRawItem(string userFileSystemPath, VirtualDriveBase virtualDrive, ILogger logger)
        {
            if (string.IsNullOrEmpty(userFileSystemPath))
            {
                throw new ArgumentNullException(nameof(userFileSystemPath));
            }

            if (virtualDrive == null)
            {
                throw new ArgumentNullException(nameof(virtualDrive));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            this.userFileSystemPath = userFileSystemPath;
            this.virtualDrive = virtualDrive;
            this.logger = logger;

            this.eTagManager = virtualDrive.GetETagManager(userFileSystemPath);
        }

        //$<PlaceholderFolder.CreatePlaceholders
        /// <summary>
        /// Creates new file and folder placeholders in the user file system in this folder.
        /// </summary>
        /// <param name="userFileSystemParentPath">User file system folder path in which the new items will be created.</param>
        /// <param name="newItemsInfo">Array of new files and folders.</param>
        /// <returns>Number of items created.</returns>
        public async Task<uint> CreateAsync(FileSystemItemMetadata[] newItemsInfo)
        {
            string userFileSystemParentPath = userFileSystemPath;

            try
            {
                // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                // Here we also check that the folder content was loaded into user file system (the folder is not offline).
                if (Directory.Exists(userFileSystemParentPath)
                    && !new DirectoryInfo(userFileSystemParentPath).Attributes.HasFlag(System.IO.FileAttributes.Offline))
                {
                    // Here we save current user file system path inside file/folder. 
                    // If the file/folder is moved or renamed while this app is not running, 
                    // we extract the saved path when this app starts and sync the file/folder to the remote storage.
                    foreach (var newItemInfo in newItemsInfo)
                    {
                        string userFileSystemPath = Path.Combine(userFileSystemParentPath, newItemInfo.Name);
                        newItemInfo.CustomData = new CustomData
                        {
                            OriginalPath = userFileSystemPath
                        }.Serialize();
                    }

                    // Create placeholders.
                    uint created = new PlaceholderFolder(userFileSystemParentPath).CreatePlaceholders(newItemsInfo);

                    // Create ETags.
                    foreach (FileSystemItemMetadata child in newItemsInfo)
                    {
                        string userFileSystemNewItemPath = Path.Combine(userFileSystemParentPath, child.Name);
                        await virtualDrive.GetETagManager(userFileSystemNewItemPath).SetETagAsync(child.ETag);

                        // Set the read-only attribute and all custom columns data.
                        UserFileSystemRawItem newUserFileSystemRawItem = virtualDrive.GetUserFileSystemRawItem(userFileSystemNewItemPath, logger);
                        await newUserFileSystemRawItem.SetLockedByAnotherUserAsync(child.LockedByAnotherUser);
                        await newUserFileSystemRawItem.SetCustomColumnsDataAsync(child.CustomProperties);
                    }

                    return created;
                }
            }
            catch (ExistsException ex)
            {
                // "Cannot create a file when that file already exists." 
                //await new UserFileSystemItem(userFileSystemParentPath).ShowDownloadErrorAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }

            return 0;
        }
        //$>        

        //$<PlaceholderItem.SetItemInfo
        /// <summary>
        /// Updates information about the file or folder placeholder in the user file system. 
        /// This method automatically hydrates and dehydrate files.
        /// </summary>
        /// <remarks>This method failes if the file or folder in user file system is modified (not in-sync with the remote storage).</remarks>
        /// <param name="itemInfo">New file or folder info.</param>
        /// <returns>True if the file was updated. False - otherwise.</returns>
        public async Task<bool> UpdateAsync(FileSystemItemMetadata itemInfo)
        {
            try
            {
                // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                if (FsPath.Exists(userFileSystemPath))
                {
                    PlaceholderItem placeholderItem = PlaceholderItem.GetItem(userFileSystemPath);

                    // To be able to update the item we need to remove the read-only attribute.
                    if ((FsPath.GetFileSystemItem(userFileSystemPath).Attributes | System.IO.FileAttributes.ReadOnly) != 0)
                    {
                        FsPath.GetFileSystemItem(userFileSystemPath).Attributes &= ~System.IO.FileAttributes.ReadOnly;
                    }

                    // Dehydrate/hydrate the file, update file size, custom data, creation date, modification date, attributes.
                    placeholderItem.SetItemInfo(itemInfo);

                    // Set ETag.
                    await eTagManager.SetETagAsync(itemInfo.ETag);

                    // Clear icon.
                    //await ClearStateAsync();

                    // Set the read-only attribute and all custom columns data.
                    await SetLockedByAnotherUserAsync(itemInfo.LockedByAnotherUser);
                    await SetCustomColumnsDataAsync(itemInfo.CustomProperties);

                    return true;
                }
            }
            catch (Exception ex)
            {
                await SetDownloadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
            return false;
        }
        //$>

        /// <summary>
        /// Deletes a file or folder placeholder in user file system.
        /// </summary>
        /// <remarks>
        /// This method throws <see cref="ConflictException"/> if the file or folder or any file or folder 
        /// in the folder hierarchy being deleted in user file system is modified (not in sync with the remote storage).
        /// </remarks>
        /// <returns>True if the file was deleted. False - otherwise.</returns>
        public async Task<bool> DeleteAsync()
        {
            // Cloud Filter API does not provide a function to delete a placeholder file only if it is not modified.
            // Here we check that the file is not modified in user file system, using GetInSync() call.
            // To avoid the file modification between GetInSync() call and Delete() call we 
            // open it without FileShare.Write flag.
            try
            {
                // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                if (FsPath.Exists(userFileSystemPath))
                {
                    using (WindowsFileSystemItem userFileSystemWinItem = WindowsFileSystemItem.Open(userFileSystemPath, (FileAccess)0, FileMode.Open, FileShare.Read | FileShare.Delete))
                    {
                        if (PlaceholderItem.GetInSync(userFileSystemWinItem.SafeHandle))
                        {
                            if (FsPath.IsFile(userFileSystemPath))
                            {
                                File.Delete(userFileSystemPath);
                            }
                            else
                            {
                                Directory.Delete(userFileSystemPath, true);
                            }

                            // Delete ETag
                            logger.LogMessage("Deleting ETag", userFileSystemPath);
                            eTagManager.DeleteETag();

                            return true;
                        }
                        else
                        {
                            throw new ConflictException(Modified.Client, "The item is not in-sync with the cloud.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await SetDownloadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }

            return false;
        }

        /// <summary>
        /// Moves a file or folder placeholder in user file system.
        /// </summary>
        /// <param name="userFileSystemNewPath">New path in user file system.</param>
        /// <remarks>
        /// This method failes if the file or folder in user file system is modified (not in sync with the remote storage)
        /// or if the target file exists.
        /// </remarks>
        /// <returns>True if the item was moved. False - otherwise.</returns>
        public async Task<bool> MoveToAsync(string userFileSystemNewPath)
        {
            // Cloud Filter API does not provide a function to move a placeholder file only if it is not modified.
            // The file may be modified between InSync call, Move() call and SetInSync() in this method.
            bool itemMoved = false;
            try
            {
                // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                if (FsPath.Exists(userFileSystemPath))
                {
                    bool inSync = PlaceholderItem.GetItem(userFileSystemPath).GetInSync();
                    if (inSync)
                    {
                        logger.LogMessage("Moving ETag", userFileSystemPath, userFileSystemNewPath);
                        await eTagManager.MoveToAsync(userFileSystemNewPath);

                        logger.LogMessage("Moving item", userFileSystemPath, userFileSystemNewPath);
                        Directory.Move(userFileSystemPath, userFileSystemNewPath);

                        // The file is marked as not in sync after move/rename. Marking it as in-sync.
                        PlaceholderItem placeholderItem = PlaceholderItem.GetItem(userFileSystemNewPath);
                        placeholderItem.SetInSync(true);
                        placeholderItem.SetOriginalPath(userFileSystemNewPath);

                        await virtualDrive.GetUserFileSystemRawItem(userFileSystemNewPath, logger).ClearStateAsync();
                        itemMoved = true;
                    }

                    if (!inSync)
                    {
                        throw new ConflictException(Modified.Client, "The item is not in-sync with the cloud.");
                    }
                }
            }
            catch (Exception ex)
            {
                string userFileSystemExPath = FsPath.Exists(userFileSystemNewPath) ? userFileSystemNewPath : userFileSystemPath;
                await virtualDrive.GetUserFileSystemRawItem(userFileSystemExPath, logger).SetDownloadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
            return itemMoved;
        }

        /// <summary>
        /// Returns true if hydration is required. False - otherwise.
        /// </summary>
        internal bool HydrationRequired()
        {
            if (FsPath.IsFile(userFileSystemPath))
            {
                int attributes = (int)new FileInfo(userFileSystemPath).Attributes;

                // Download content (hydrate) if file is pinned and no file content is present locally (offline).
                if (((attributes & (int)FileAttributesExt.Pinned) != 0)
                    && ((attributes & (int)FileAttributesExt.Offline) != 0))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if dehydration is required. False - otherwise.
        /// </summary>
        internal bool DehydrationRequired()
        {
            if (FsPath.IsFile(userFileSystemPath))
            {
                int attributes = (int)new FileInfo(userFileSystemPath).Attributes;

                // Remove content (dehydrate) if file is unpinned and file content is present locally (not offline).
                if (((attributes & (int)FileAttributesExt.Unpinned) != 0)
                    && ((attributes & (int)FileAttributesExt.Offline) == 0))
                {
                    return true;
                }
            }
            return false;
        }

        internal async Task ClearStateAsync()
        {
            if (FsPath.Exists(userFileSystemPath))
            {
                await SetIconAsync(false);
            }
        }

        internal async Task SetUploadErrorStateAsync(Exception ex)
        {
            if (FsPath.Exists(userFileSystemPath))
            {
                if (ex is ConflictException)
                {
                    await SetConflictIconAsync(true);
                }
                else
                {
                    await SetUploadPendingIconAsync(true);
                }
            }
        }

        private async Task SetDownloadErrorStateAsync(Exception ex)
        {
            if (FsPath.Exists(userFileSystemPath))
            {
                if (ex is ConflictException)
                {
                    await SetConflictIconAsync(true);
                }
                if (ex is ExistsException)
                {
                    await SetConflictIconAsync(true);
                }
                else
                {
                    // Could be BlockedException or other exception.
                    await SetDownloadPendingIconAsync(true);
                }
            }
        }

        /// <summary>
        /// Sets or removes "Conflict" icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        private async Task SetConflictIconAsync(bool set)
        {
            await SetIconAsync(set, (int)CustomColumnIds.ConflictIcon, "Error.ico", "Conflict. File is modified both on the server and on the client.");
        }

        /// <summary>
        /// Sets or removes "Download pending" icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        private async Task SetDownloadPendingIconAsync(bool set)
        {
            //            await SetIconAsync(set, 2, "Down.ico", "Download from server pending");
        }

        /// <summary>
        /// Sets or removes "Upload pending" icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        private async Task SetUploadPendingIconAsync(bool set)
        {
            //            await SetIconAsync(set, 2, "Up.ico", "Upload to server pending");
        }

        /// <summary>
        /// Sets or removes "Lock" icon and all lock properties.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        internal async Task SetLockInfoAsync(ServerLockInfo lockInfo)
        {
            IEnumerable<FileSystemItemPropertyData> lockProps = null;
            if (lockInfo != null)
            {
                lockProps = lockInfo.GetLockProperties(Path.Combine(virtualDrive.Settings.IconsFolderPath, "Locked.ico"));
            }
            await SetCustomColumnsDataAsync(lockProps);
        }

        /// <summary>
        /// Sets or removes "Lock pending" icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        internal async Task SetLockPendingIconAsync(bool set)
        {
            await SetIconAsync(set, (int)CustomColumnIds.LockOwnerIcon, "LockedPending.ico", "Updating lock...");
        }

        /// <summary>
        /// Sets or removes icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        private async Task SetIconAsync(bool set, int? id = null, string iconFile = null, string description = null)
        {
            IStorageItem storageItem = await FsPath.GetStorageItemAsync(userFileSystemPath);

            if (storageItem == null)
            {
                // This method may be called on temp files, typically created by MS Office, that exist for a short period of time.
                // StorageProviderItemProperties.SetAsync(null,) causes AccessViolationException 
                // which is not handled by .NET (or handled by HandleProcessCorruptedStateExceptions) and causes a fatal crush.
                return;
            }

            try
            {
                if (set)
                {
                    StorageProviderItemProperty propState = new StorageProviderItemProperty()
                    {
                        Id = id.Value,
                        Value = description,
                        IconResource = Path.Combine(virtualDrive.Settings.IconsFolderPath, iconFile)
                    };
                    await StorageProviderItemProperties.SetAsync(storageItem, new StorageProviderItemProperty[] { propState });
                }
                else
                {
                    await StorageProviderItemProperties.SetAsync(storageItem, new StorageProviderItemProperty[] { });
                }
            }

            // Setting status icon failes for blocked files.
            catch (FileNotFoundException)
            {

            }
            catch (COMException)
            {
                // "Error HRESULT E_FAIL has been returned from a call to a COM component."
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147024499)
                {
                    // "The operation failed due to a conflicting cloud file property lock. (0x8007018D)"
                }
                else
                {
                    // Rethrow the exception preserving stack trace of the original exception.
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
                }
            }
        }

        /// <summary>
        /// Sets or removes read-only attribute on files.
        /// </summary>
        /// <param name="set">True to set the read-only attribute. False - to remove the read-only attribute.</param>
        public async Task<bool> SetLockedByAnotherUserAsync(bool set)
        {
            bool resultSet = false;
            // Changing read-only attribute on folders triggers folders listing. Changing it on files only.

            if (FsPath.IsFile(userFileSystemPath))
            {
                FileInfo file = new FileInfo(userFileSystemPath);
                if (set != file.IsReadOnly)
                {
                    // Set/Remove read-only attribute.
                    if (set)
                    {
                        new FileInfo(userFileSystemPath).Attributes |= System.IO.FileAttributes.ReadOnly;
                    }
                    else
                    {
                        new FileInfo(userFileSystemPath).Attributes &= ~System.IO.FileAttributes.ReadOnly;
                    }

                    resultSet = true;
                }
            }

            return resultSet;
        }

        internal async Task SetCustomColumnsDataAsync(IEnumerable<FileSystemItemPropertyData> customColumnsData)
        {
            List<StorageProviderItemProperty> customColumns = new List<StorageProviderItemProperty>();

            if (customColumnsData != null)
            {
                foreach (FileSystemItemPropertyData column in customColumnsData)
                {
                    customColumns.Add(new StorageProviderItemProperty()
                    {
                        Id = column.Id,
                        // If value is empty Windows File Manager crushes.
                        Value = string.IsNullOrEmpty(column.Value) ? "-" : column.Value,
                        // If icon is not set Windows File Manager crushes.
                        IconResource = column.IconResource ?? Path.Combine(virtualDrive.Settings.IconsFolderPath, "Blank.ico")
                    });
                }
            }

            // This method may be called on temp files, typically created by MS Office, that exist for a short period of time.          
            IStorageItem storageItem = await FsPath.GetStorageItemAsync(userFileSystemPath);
            if (storageItem == null)
            {
                // This method may be called on temp files, typically created by MS Office, that exist for a short period of time.
                // StorageProviderItemProperties.SetAsync(null,) causes AccessViolationException 
                // which is not handled by .NET (or handled by HandleProcessCorruptedStateExceptions) and causes a fatal crush.
                return;
            }

            FileInfo file = new FileInfo(userFileSystemPath);

            // Can not set provider properties on read-only files.
            // Changing read-only attribute on folders triggers folders listing. Changing it on files only.
            bool readOnly = file.IsReadOnly;

            // Remove read-only attribute.
            if (readOnly && ((file.Attributes & System.IO.FileAttributes.Directory) == 0))
            {
                file.IsReadOnly = false;
                //new FileInfo(userFileSystemPath).Attributes &= ~System.IO.FileAttributes.ReadOnly;
            }

            // Update columns data.
            await StorageProviderItemProperties.SetAsync(storageItem, customColumns);

            // Set read-only attribute.
            if (readOnly && ((file.Attributes & System.IO.FileAttributes.Directory) == 0))
            {
                file.IsReadOnly = true;
                //new FileInfo(userFileSystemPath).Attributes |= System.IO.FileAttributes.ReadOnly;
            }
        }
    }
}
