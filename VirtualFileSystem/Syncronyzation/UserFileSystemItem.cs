using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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

        /// <summary>
        /// Compares user file system item and remote storage item. Returns true if items are equal. False - otherwise.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <returns>Returns true user file system item and remote storage item are equal. False - otherwise.</returns>
        public async Task<bool> EqualsAsync(FileSystemInfo remoteStorageItem)
        {
            // Here you will typically compare file ETags. 
            // For the sake of simplicity we just compare LastWriteTime.

            long remoteStorageETag = remoteStorageItem.LastWriteTime.ToBinary();

            // We store remote storage LastWriteTime inside custom data when creating and updating files/folders.
            byte[] userFileSystemItemCustomData = PlaceholderItem.GetItem(userFileSystemPath).GetCustomData();
            long userFileSystemETag = BitConverter.ToInt64(userFileSystemItemCustomData);

            return remoteStorageETag == userFileSystemETag;
        }

        /// <summary>
        /// Creates a new file or folder placeholder in user file system.
        /// </summary>
        /// <param name="userFileSystemParentPath">User file system folder path in which the new item will be created.</param>
        /// <param name="remoteStorageItem">Remote storage item info. The new placeholder will be populated with data from this item.</param>
        public static async Task CreateAsync(string userFileSystemParentPath, FileSystemInfo remoteStorageItem)
        {
            IFileSystemItemBasicInfo userFileSystemNewItemInfo = Mapping.GetUserFileSysteItemInfo(remoteStorageItem);

            try
            {
                new PlaceholderFolder(userFileSystemParentPath).CreatePlaceholders(new[] { userFileSystemNewItemInfo });
            }
            catch(Win32Exception ex)
            {
                // "Cannot create a file when that file already exists."
                if (ex.NativeErrorCode == 183)
                {
                    // Process conflict.

                    // Rethrow the exception preserving stack trace of the original exception.
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
                    throw ex; // This is for the compiler to know that the code never reaches here.
                }
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
            /*
            // Process conflicts. File <-> Folder. Folder <-> File.
            if (File.Exists(userFileSystemPath) && (remoteStorageItem is DirectoryInfo))
            {
            }
            else if (Directory.Exists(userFileSystemPath) && (remoteStorageItem is FileInfo))
            {
            }
            */
            FileSystemItemBasicInfo userFileSystemItemInfo = Mapping.GetUserFileSysteItemInfo(remoteStorageItem);

            try
            {
                // Dehydrate/hydrate the file, update file size and info.
                PlaceholderItem.GetItem(userFileSystemPath).SetItemInfo(userFileSystemItemInfo);

                // Clear download pending icon
                await SetDownloadPendingIconAsync(false);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                if (ex.NativeErrorCode == 802)
                {
                    // The file is blocked by the client application or pinned.
                    // "The operation did not complete successfully because it would cause an oplock to be broken. 
                    // The caller has requested that existing oplocks not be broken."

                    // Show download pending icon.
                    await SetDownloadPendingIconAsync(true);
                }

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }

        /// <summary>
        /// Hydrates or dehydrates the file depending on pinned/unpinned attributes and offline attribute setting.
        /// </summary>
        /// <returns>
        /// Returns true if hydration is competed, false if dehydration is completed 
        /// or null if no hydration/dehydrations was done.
        /// </returns>
        internal async Task<bool?> UpdateHydrationAsync()
        {
            if (FsPath.IsFile(userFileSystemPath))
            {
                int attributes = (int)new FileInfo(userFileSystemPath).Attributes;

                // Download content (hydrate) if file is pinned and no file content is present locally (offline).
                if (((attributes & (int)FileAttributesExt.Pinned) != 0)
                    && ((attributes & (int)FileAttributesExt.Offline) != 0))
                {
                    new PlaceholderFile(userFileSystemPath).Hydrate(0, -1);
                    return true;
                }

                // Remove content (dehydrate) if file is unpinned and file content is present locally (not offline).
                else if (((attributes & (int)FileAttributesExt.Unpinned) != 0)
                       && ((attributes & (int)FileAttributesExt.Offline) == 0))
                {
                    new PlaceholderFile(userFileSystemPath).Dehydrate(0, -1, false);
                    return false;
                }
            }
            return null;
        }

        /// <summary>
        /// Sets or removes "Download pending" icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        private async Task SetDownloadPendingIconAsync(bool set)
        {
            IStorageItem storageItem = await FsPath.GetStorageItemAsync(userFileSystemPath);

            if (set)
            {
                // Set upload pending icon.

                // Set download pending icon.
                StorageProviderItemProperty propState = new StorageProviderItemProperty()
                {
                    Id = 2,
                    Value = "Download from server pending", // "@propsys.dll,-42291",
                    IconResource = "%systemroot%\\system32\\imageres.dll,-1402"
                };
                await StorageProviderItemProperties.SetAsync(storageItem, new[] { propState });
            }
            else
            {
                await StorageProviderItemProperties.SetAsync(storageItem, new StorageProviderItemProperty[] { });
            }
        }

        /// <summary>
        /// Deletes a file or folder placeholder in user file system.
        /// </summary>
        /// <remarks>This method failes if the file or folder in user file system is modified (not in sync with the remote storage).</remarks>
        public async Task DeleteAsync()
        {
            // Windows does not provide a function to delete a placeholder file only if it is not modified.
            // Here we check that the file is not modified in user file system, using GetInSync() call.
            // To avoid the file modification between GetInSync() call and Delete() call in this method we 
            // open it without FileShare.Write flag.
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
                    throw new IOException("File not in-sync.");
                }
            }
        }

        /// <summary>
        /// Moves a file or folder placeholder in user file system.
        /// </summary>
        /// <param name="newUserFileSystemPath">New path in user file system.</param>
        /// <remarks>
        /// This method failes if the file or folder in user file system is modified (not in sync with the remote storage)
        /// or if the target file exists.
        /// </remarks>
        public async Task MoveAsync(string newUserFileSystemPath)
        {
            // Windows does not provide a function to move a placeholder file only if it is not modified.
            // The file may be modified between InSync call, Move() call and SetInSync() in this method.

            PlaceholderItem oldUserFileSystemItem = PlaceholderItem.GetItem(userFileSystemPath);

            if (oldUserFileSystemItem.GetInSync())
            {
                Directory.Move(userFileSystemPath, newUserFileSystemPath);

                // The file is marked as not in sync after move/rename. Marking it as in-sync.
                PlaceholderItem.GetItem(newUserFileSystemPath).SetInSync(true);
            }
            else
            {
                throw new IOException("File not in-sync.");
            }
        }
    }
}
