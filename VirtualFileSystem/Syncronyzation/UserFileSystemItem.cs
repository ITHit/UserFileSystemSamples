using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
    internal class UserFileSystemItem
    {
        /// <summary>
        /// Path to the file or folder placeholder in user file system.
        /// </summary>
        private string userFileSystemPath;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">File or folder path in user file system.</param>
        internal UserFileSystemItem(string userFileSystemPath)
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

        /// <summary>
        /// Returns true if the remote storage ETag and user file system ETags are equal. False - otherwise.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <remarks>
        /// ETag is updated on the server during every fdocument update and is stored with a file in 
        /// user file system. It is sent back to the remote storage togather with a modified content. 
        /// This makes sure the changes on the server is not overwritten if the document on the server is modified.
        /// </remarks>
        internal async Task<bool> ETagEqualsAsync(FileSystemInfo remoteStorageItem)
        {
            string remoteStorageETag = remoteStorageItem.LastWriteTime.ToBinary().ToString();

            // Intstead of ETag we store remote storage LastWriteTime inside custom data when 
            // creating and updating files/folders.
            string userFileSystemETag = PlaceholderItem.GetItem(userFileSystemPath).GetETag();
            if(string.IsNullOrEmpty(userFileSystemETag))
            {
                // No ETag associated with the file. The file either was just created in user file system 
                // or folder where user file system was created is not empty.
                return false;
            }

            return remoteStorageETag == userFileSystemETag;
        }

        /// <summary>
        /// Creates a new file or folder placeholder in user file system.
        /// </summary>
        /// <param name="userFileSystemParentPath">User file system folder path in which the new item will be created.</param>
        /// <param name="remoteStorageItem">Remote storage item info. The new placeholder will be populated with data from this item.</param>
        internal static async Task CreateAsync(string userFileSystemParentPath, FileSystemInfo remoteStorageItem)
        {
            IFileSystemItemBasicInfo userFileSystemNewItemInfo = Mapping.GetUserFileSysteItemInfo(remoteStorageItem);

            try
            {
                new PlaceholderFolder(userFileSystemParentPath).CreatePlaceholders(new[] { userFileSystemNewItemInfo });
            }
            catch (ExistsException ex)
            {
                // "Cannot create a file when that file already exists." 
                //await new UserFileSystemItem(userFileSystemParentPath).ShowDownloadErrorAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }

        /// <summary>
        /// Updates information about the file or folder placeholder in the user file system. 
        /// This method automatically hydrates and dehydrate files.
        /// </summary>
        /// <remarks>This method failes if the file or folder in user file system is modified (not in-sync with the remote storage).</remarks>
        /// <param name="remoteStorageItem">Remote storage item info. The placeholder info will be updated with info provided in this item.</param>
        internal async Task UpdateAsync(FileSystemInfo remoteStorageItem)
        {
            FileSystemItemBasicInfo userFileSystemItemInfo = Mapping.GetUserFileSysteItemInfo(remoteStorageItem);

            try
            {
                // Dehydrate/hydrate the file, update file size, custom data, creation date, modification date, attributes.
                PlaceholderItem.GetItem(userFileSystemPath).SetItemInfo(userFileSystemItemInfo);

                // Clear icon.
                await ClearStateAsync();
            }
            catch (Exception ex)
            {
                await SetDownloadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }

        /// <summary>
        /// Deletes a file or folder placeholder in user file system.
        /// </summary>
        /// <remarks>
        /// This method throws <see cref="ConflictException"/> if the file or folder or any file or folder 
        /// in the folder hierarchy being deleted in user file system is modified (not in sync with the remote storage).
        /// </remarks>
        public async Task DeleteAsync()
        {
            // Cloud Filter API does not provide a function to delete a placeholder file only if it is not modified.
            // Here we check that the file is not modified in user file system, using GetInSync() call.
            // To avoid the file modification between GetInSync() call and Delete() call we 
            // open it without FileShare.Write flag.
            try
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
                    }
                    else
                    {
                        throw new ConflictException(Modified.Client, "The item is not in-sync with the cloud.");
                    }
                }
            }
            catch (Exception ex)
            {
                await SetDownloadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }

        /// <summary>
        /// Moves a file or folder placeholder in user file system.
        /// </summary>
        /// <param name="userFileSystemNewPath">New path in user file system.</param>
        /// <remarks>
        /// This method failes if the file or folder in user file system is modified (not in sync with the remote storage)
        /// or if the target file exists.
        /// </remarks>
        public async Task MoveAsync(string userFileSystemNewPath)
        {
            // Cloud Filter API does not provide a function to move a placeholder file only if it is not modified.
            // The file may be modified between InSync call, Move() call and SetInSync() in this method.

            try
            {
                PlaceholderItem oldUserFileSystemItem = PlaceholderItem.GetItem(userFileSystemPath);

                if (oldUserFileSystemItem.GetInSync())
                {
                    Directory.Move(userFileSystemPath, userFileSystemNewPath);

                    // The file is marked as not in sync after move/rename. Marking it as in-sync.
                    PlaceholderItem.GetItem(userFileSystemNewPath).SetInSync(true);
                    PlaceholderItem.GetItem(userFileSystemNewPath).SetOriginalPath(userFileSystemNewPath);
                    await new UserFileSystemItem(userFileSystemNewPath).ClearStateAsync();
                }
                else
                {
                    throw new ConflictException(Modified.Client, "The item is not in-sync with the cloud.");
                }
            }
            catch (Exception ex)
            {
                string path = FsPath.Exists(userFileSystemNewPath) ? userFileSystemNewPath : userFileSystemPath;
                await new UserFileSystemItem(userFileSystemPath).SetDownloadErrorStateAsync(ex);

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
            await SetIconAsync(set, 2, "Down.ico", "Download from server pending");
        }

        /// <summary>
        /// Sets or removes "Upload pending" icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        private async Task SetUploadPendingIconAsync(bool set)
        {
            await SetIconAsync(set, 2, "Up.ico", "Upload to server pending");
        }

        /// <summary>
        /// Sets or removes icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        private async Task SetIconAsync(bool set, int? id = null, string iconFile = null, string description = null)
        {
            IStorageItem storageItem = await FsPath.GetStorageItemAsync(userFileSystemPath);
            try
            {
                if (set)
                {
                    StorageProviderItemProperty propState = new StorageProviderItemProperty()
                    {
                        Id = id.Value,
                        Value = description,
                        IconResource = Path.Combine(Program.IconsFolderPath, iconFile)
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
