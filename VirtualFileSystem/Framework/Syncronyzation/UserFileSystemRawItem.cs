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

namespace VirtualFileSystem.Syncronyzation
{
    /// <summary>
    /// Provides methods for synching from remote storage to user file system.
    /// Creates, updates and delets placeholder files and folders based on the info from remote storage.
    /// </summary>
    internal class UserFileSystemRawItem
    {
        /// <summary>
        /// Path to the file or folder placeholder in user file system.
        /// </summary>
        private string userFileSystemPath;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">File or folder path in user file system.</param>
        /// <param name="logger">Logger.</param>
        internal UserFileSystemRawItem(string userFileSystemPath)
        {
            if(string.IsNullOrEmpty(userFileSystemPath))
            {
                throw new ArgumentNullException("userFileSystemPath");
            }

            this.userFileSystemPath = userFileSystemPath;
        }

        /*
        /// <summary>
        /// Compares user file system item and remote storage item. Returns true if items are equal. False - otherwise.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <returns>Returns true if user file system item and remote storage item are equal. False - otherwise.</returns>
        public async Task<bool> EqualsAsync(FileSystemInfo remoteStorageItem)
        {
            // Here you will typically compare file content.
            // For the sake of simplicity we just compare LastWriteTime.
            return remoteStorageItem.LastWriteTime.Equals(FsPath.GetFileSystemItem(userFileSystemPath).LastWriteTime);
        }
        */

        //$<PlaceholderFolder.CreatePlaceholders
        /// <summary>
        /// Creates a new file or folder placeholder in user file system.
        /// </summary>
        /// <param name="userFileSystemParentPath">User file system folder path in which the new item will be created.</param>
        /// <param name="newItemsInfo">Array of new files and folders.</param>
        /// <returns>Number of items created.</returns>
        internal static async Task<uint> CreateAsync(string userFileSystemParentPath, FileSystemItemBasicInfo[] newItemsInfo)
        {
            try
            {
                // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                // Here we also check that the folder content was loaded into user file system (the folder is not offline).
                if (Directory.Exists(userFileSystemParentPath)
                    && !new DirectoryInfo(userFileSystemParentPath).Attributes.HasFlag(System.IO.FileAttributes.Offline))
                {
                    // Create placeholders.
                    uint created = new PlaceholderFolder(userFileSystemParentPath).CreatePlaceholders(newItemsInfo);

                    // Create ETags.
                    foreach (FileSystemItemBasicInfo child in newItemsInfo)
                    {
                        string userFileSystemItemPath = Path.Combine(userFileSystemParentPath, child.Name);
                        await ETag.SetETagAsync(userFileSystemItemPath, child.ETag);
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
        internal async Task<bool> UpdateAsync(FileSystemItemBasicInfo itemInfo)
        {
            try
            {
                // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                if (FsPath.Exists(userFileSystemPath))
                {
                    PlaceholderItem placeholderItem = PlaceholderItem.GetItem(userFileSystemPath);

                    // Dehydrate/hydrate the file, update file size, custom data, creation date, modification date, attributes.
                    placeholderItem.SetItemInfo(itemInfo);

                    // Set ETag.
                    await ETag.SetETagAsync(userFileSystemPath, itemInfo.ETag);

                    // Clear icon.
                    await ClearStateAsync();

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
                            ETag.DeleteETag(userFileSystemPath);

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
        public async Task MoveToAsync(string userFileSystemNewPath)
        {
            // Cloud Filter API does not provide a function to move a placeholder file only if it is not modified.
            // The file may be modified between InSync call, Move() call and SetInSync() in this method.

            try
            {
                // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                if (FsPath.Exists(userFileSystemPath))
                {
                    bool inSync = PlaceholderItem.GetItem(userFileSystemPath).GetInSync();
                    if (inSync)
                    {
                        string eTag = await ETag.GetETagAsync(userFileSystemPath);

                        ETag.DeleteETag(userFileSystemPath);
                        try
                        {
                            Directory.Move(userFileSystemPath, userFileSystemNewPath);
                        }
                        catch
                        {
                            await ETag.SetETagAsync(userFileSystemPath, eTag);
                            throw;
                        }

                        await ETag.SetETagAsync(userFileSystemNewPath, eTag);

                        // The file is marked as not in sync after move/rename. Marking it as in-sync.
                        PlaceholderItem placeholderItem = PlaceholderItem.GetItem(userFileSystemNewPath);
                        placeholderItem.SetInSync(true);
                        placeholderItem.SetOriginalPath(userFileSystemNewPath);

                        await new UserFileSystemRawItem(userFileSystemNewPath).ClearStateAsync();
                    }

                    if(!inSync)
                    {
                        throw new ConflictException(Modified.Client, "The item is not in-sync with the cloud.");
                    }
                }
            }
            catch (Exception ex)
            {
                string path = FsPath.Exists(userFileSystemNewPath) ? userFileSystemNewPath : userFileSystemPath;
                await new UserFileSystemRawItem(userFileSystemPath).SetDownloadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
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
            await SetIconAsync(set, 2, "Error.ico", "Conflict. File is modified both on the server and on the client.");
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
        /// Sets or removes "Lock" icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        internal async Task SetLockIconAsync(bool set)
        {
            await SetIconAsync(set, 2, "Locked.ico", "The item is locked");
        }

        /// <summary>
        /// Sets or removes "Lock pending" icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        internal async Task SetLockPendingIconAsync(bool set)
        {
            await SetIconAsync(set, 2, "LockedPending.ico", "Updating lock...");
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
                return; // Item does not exists.
            }

            try
            {
                if (set)
                {
                    StorageProviderItemProperty propState = new StorageProviderItemProperty()
                    {
                        Id = id.Value,
                        Value = description,
                        IconResource = Path.Combine(Program.Settings.IconsFolderPath, iconFile)
                    };
                    await StorageProviderItemProperties.SetAsync(storageItem, new StorageProviderItemProperty[] { propState });
                }
                else
                {
                    await StorageProviderItemProperties.SetAsync(storageItem, new StorageProviderItemProperty[] { });
                }
            }

            // Setting status icon failes for blocked files.
            catch(FileNotFoundException)
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
    }
}
