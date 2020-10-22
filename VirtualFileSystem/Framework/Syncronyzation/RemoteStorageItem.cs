using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace VirtualFileSystem.Syncronyzation
{
    /// <summary>
    /// Provides methods for synching user file system to remote storage.
    /// Creates, updates and delets files and folders based on the info from user file system.
    /// </summary>
    public class RemoteStorageItem
    {
        /// <summary>
        /// Path to the file or folder in the user file system.
        /// </summary>
        private string userFileSystemPath;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">File or folder path in the user file system.</param>
        internal RemoteStorageItem(string userFileSystemPath)
        {
            if (string.IsNullOrEmpty(userFileSystemPath))
            {
                throw new ArgumentNullException("userFileSystemPath");
            }

            this.userFileSystemPath = userFileSystemPath;
        }

        public static async Task CreateAsync(string userFileSystemNewItemPath)
        {
            try
            {
                await new RemoteStorageItem(userFileSystemNewItemPath).CreateOrUpdateAsync(FileMode.CreateNew);

                await new UserFileSystemItem(userFileSystemNewItemPath).ClearStateAsync();

            }
            catch(Exception ex)
            {
                await new UserFileSystemItem(userFileSystemNewItemPath).SetUploadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }

        public async Task UpdateAsync()
        {
            try
            {
                await CreateOrUpdateAsync(FileMode.Open);

                await new UserFileSystemItem(userFileSystemPath).ClearStateAsync();
            }
            catch(Exception ex)
            {
                await new UserFileSystemItem(userFileSystemPath).SetUploadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }

        private async Task CreateOrUpdateAsync(FileMode mode)
        {
            if ( (mode!=FileMode.CreateNew) && (mode !=FileMode.Open) )
            {
                throw new ArgumentOutOfRangeException("mode", $"Must be {FileMode.CreateNew} or {FileMode.Open}");
            }

            FileSystemInfo userFileSystemItem = FsPath.GetFileSystemItem(userFileSystemPath);

            using (WindowsFileSystemItem userFileSystemWinItem = WindowsFileSystemItem.OpenReadAttributes(userFileSystemPath, FileMode.Open, FileShare.Read))
            //await using (FileStream userFileSystemStream = userFileSystemFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // Create the new file/folder in the remote storage only if the file/folder in the user file system was not moved.
                // If the file is moved in user file system, move must first be syched to remote storage.
                if ((mode == FileMode.CreateNew) && PlaceholderItem.GetItem(userFileSystemPath).IsMoved())
                {
                    string originalPath = PlaceholderItem.GetItem(userFileSystemPath).GetOriginalPath();
                    throw new ConflictException(Modified.Client, $"The item was moved. Original path: {originalPath}");
                }

                // Ensures LastWriteTimeUtc is in sync with file content after Open() was called.
                userFileSystemItem.Refresh(); 

                IFileSystemItemBasicInfo info = GetBasicInfo(userFileSystemItem);

                // Update remote storage file.
                FileStream userFileSystemStream = null;
                try
                {
                    string eTag = null;
                    if (FsPath.IsFile(userFileSystemPath))
                    {
                        // File is marked as not in-sync when updated OR moved. 
                        // Opening a file for reading triggers hydration, open only if content is modified.
                        if (PlaceholderFile.GetFileDataSizeInfo(userFileSystemWinItem.SafeHandle).ModifiedDataSize > 0)
                        {
                            //userFileSystemStream = new FileStream(userFileSystemWinItem.SafeHandle, FileAccess.Read);
                            userFileSystemStream = ((FileInfo)userFileSystemItem).Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                        }
                        if (mode == FileMode.CreateNew)
                        {
                            string userFileSystemParentPath = Path.GetDirectoryName(userFileSystemPath);
                            eTag = await new UserFolder(userFileSystemParentPath).CreateFileAsync((IFileBasicInfo)info, userFileSystemStream);
                        }
                        else
                        {
                            eTag = await new UserFile(userFileSystemPath).UpdateAsync((IFileBasicInfo)info, userFileSystemStream);
                        }
                    }
                    else
                    {
                        if (mode == FileMode.CreateNew)
                        {
                            string userFileSystemParentPath = Path.GetDirectoryName(userFileSystemPath);
                            eTag = await new UserFolder(userFileSystemParentPath).CreateFolderAsync((IFolderBasicInfo)info);
                        }
                        else
                        {
                            eTag = await new UserFolder(userFileSystemPath).UpdateAsync((IFolderBasicInfo)info);
                        }
                    }
                    await ETag.SetETagAsync(userFileSystemPath, eTag);
                    if (mode == FileMode.CreateNew)
                    {
                        PlaceholderItem.GetItem(userFileSystemPath).SetOriginalPath(userFileSystemPath);
                    }
                }
                finally
                {
                    if (userFileSystemStream != null)
                    {
                        userFileSystemStream.Close();
                    }
                }
                                
                PlaceholderItem.SetInSync(userFileSystemWinItem.SafeHandle, true);
            }
        }

        /// <summary>
        /// Gets file system item info from the user file system item. 
        /// Removes Pinned, Unpinned and Offline flags from attributes.
        /// </summary>
        /// <param name="userFileSystemItem">User file system item info.</param>
        /// <returns><see cref="FileSystemItemBasicInfo"/> object.</returns>
        private static IFileSystemItemBasicInfo GetBasicInfo(FileSystemInfo userFileSystemItem)
        {
            FileSystemItemBasicInfo itemInfo;

            if (userFileSystemItem is FileInfo)
            {
                itemInfo = new FileBasicInfo();
            }
            else
            {
                itemInfo = new FolderBasicInfo();
            }

            // Remove Pinned, unpinned and offline flags,
            // so they do not occasionally go into the remote storage.
            FileAttributes flags = userFileSystemItem.Attributes
                & (FileAttributes)~FileAttributesExt.Pinned
                & (FileAttributes)~FileAttributesExt.Unpinned
                & (FileAttributes)~FileAttributesExt.Offline;

            itemInfo.Name = userFileSystemItem.Name;
            itemInfo.Attributes = flags;
            itemInfo.CreationTime = userFileSystemItem.CreationTime;
            itemInfo.LastWriteTime = userFileSystemItem.LastWriteTime;
            itemInfo.LastAccessTime = userFileSystemItem.LastAccessTime;
            itemInfo.ChangeTime = userFileSystemItem.LastWriteTime;

            itemInfo.CustomData = PlaceholderItem.GetItem(userFileSystemItem.FullName).GetCustomData();

            if (userFileSystemItem is FileInfo)
            {
                ((FileBasicInfo)itemInfo).Length = ((FileInfo)userFileSystemItem).Length;
            };

            return itemInfo;
        }
        internal async Task MoveToAsync(string userFileSystemNewPath, IConfirmationResultContext resultContext = null)
        {
            string userFileSystemOldPath = userFileSystemPath;

            try
            {
                bool? inSync = null;
                bool updateTargetOnSuccess = false;
                string eTag = null;
                try
                {
                    if (!FsPath.IsRecycleBin(userFileSystemNewPath) // When a file is deleted, it is moved to a Recycle Bin.
                        && !FsPath.AvoidSync(userFileSystemOldPath) && !FsPath.AvoidSync(userFileSystemNewPath))
                    {
                        // Read In-Sync state before move and set after move
                        if (FsPath.Exists(userFileSystemOldPath))
                        {
                            inSync = PlaceholderItem.GetItem(userFileSystemOldPath).GetInSync();
                        }

                        eTag = await ETag.GetETagAsync(userFileSystemOldPath);
                        ETag.DeleteETag(userFileSystemOldPath);

                        await new VirtualFileSystem.UserFileSystemItem(userFileSystemOldPath).MoveToAsync(userFileSystemNewPath);
                        updateTargetOnSuccess = true;
                    }
                }
                finally
                {
                    if (resultContext != null)
                    {
                        resultContext.ReturnConfirmationResult();
                    }
                    
                    // This check is just to avoid extra error in the log.
                    if (FsPath.Exists(userFileSystemNewPath))
                    {
                        // Open file to preven reads and changes between GetFileDataSizeInfo() call and SetInSync() call.
                        using (WindowsFileSystemItem userFileSystemWinItem = WindowsFileSystemItem.OpenReadAttributes(userFileSystemNewPath, FileMode.Open, FileShare.None))
                        {
                            if ((eTag != null) && PlaceholderItem.IsPlaceholder(userFileSystemNewPath))
                            {
                                await ETag.SetETagAsync(userFileSystemNewPath, eTag);
                            }

                            // If a file with content is deleted it is moved to a recycle bin and converted
                            // to a regular file, so placeholder features are not available on it, checking if a file is a placeholder.
                            if (updateTargetOnSuccess && PlaceholderItem.IsPlaceholder(userFileSystemNewPath))
                            {
                                PlaceholderItem placeholderNew = PlaceholderItem.GetItem(userFileSystemNewPath);

                                // Update OriginalPath, so the item does not appear as moved.
                                placeholderNew.SetOriginalPath(userFileSystemNewPath);

                                if (inSync != null)
                                {
                                    placeholderNew.SetInSync(inSync.Value);
                                }
                                else if ((placeholderNew is PlaceholderFile) && ((PlaceholderFile)placeholderNew).GetFileDataSizeInfo().ModifiedDataSize == 0)
                                {
                                    placeholderNew.SetInSync(true);
                                }
                                await new UserFileSystemItem(userFileSystemNewPath).ClearStateAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string userFileSystemPath = FsPath.Exists(userFileSystemNewPath) ? userFileSystemNewPath : userFileSystemOldPath;
                await new UserFileSystemItem(userFileSystemPath).SetUploadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }

        /// <summary>
        /// Deletes file or folder in the remote storage.
        /// </summary>
        internal async Task<bool> DeleteAsync()
        {
            if (!FsPath.AvoidSync(userFileSystemPath))
            {
                await new VirtualFileSystem.UserFileSystemItem(userFileSystemPath).DeleteAsync();
                
                ETag.DeleteETag(userFileSystemPath);

                return true;
            }

            return false;
        }
    }
}
