using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;

namespace VirtualFileSystem.Syncronyzation
{
    /// <summary>
    /// Provides methods for synching the user file system to the remote storage.
    /// Creates, updates, deletes, moves, locks and unloacks files and folders based on the info from user file system.
    /// This class also іets status icons in file manager.
    /// </summary>
    /// <remarks>In most cases you can use this class in your project without any changes.</remarks>
    public class RemoteStorageRawItem
    {
        /// <summary>
        /// Path to the file or folder in the user file system.
        /// </summary>
        private string userFileSystemPath;

        /// <summary>
        /// Logger.
        /// </summary>
        private ILogger logger;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">File or folder path in the user file system.</param>
        /// <param name="logger">Logger.</param>
        internal RemoteStorageRawItem(string userFileSystemPath, ILogger logger)
        {
            if (string.IsNullOrEmpty(userFileSystemPath))
            {
                throw new ArgumentNullException("userFileSystemPath");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            this.userFileSystemPath = userFileSystemPath;
            this.logger = logger;
        }

        /// <summary>
        /// Creates a new file or folder in the remote storage.
        /// </summary>
        /// <param name="userFileSystemNewItemPath">Path to the file or folder in the user file system to be created in the remote storage.</param>
        /// <param name="logger">Logger</param>
        public static async Task CreateAsync(string userFileSystemNewItemPath, ILogger logger)
        {
            try
            {
                logger.LogMessage("Creating item in the remote storage", userFileSystemNewItemPath);
                await new RemoteStorageRawItem(userFileSystemNewItemPath, logger).CreateOrUpdateAsync(FileMode.CreateNew);
                await new UserFileSystemRawItem(userFileSystemNewItemPath).ClearStateAsync();
                logger.LogMessage("Created item in the remote storage succesefully", userFileSystemNewItemPath);
            }
            catch (Exception ex)
            {
                await new UserFileSystemRawItem(userFileSystemNewItemPath).SetUploadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }

        /// <summary>
        /// Sends contending to the remote storage if the item is modified. 
        /// If Auto-locking is enabled, automatically locks the file if not locked. Unlocks the file after the update if auto-locked.
        /// </summary>
        public async Task UpdateAsync()
        {
            Lock fileLock = null;
            try
            {
                if (!PlaceholderItem.GetItem(userFileSystemPath).GetInSync())
                {

                    // Lock file if auto-locking is enabled.
                    if (Program.Settings.AutoLock)
                    {
                        // Get existing lock or create a new lock.
                        fileLock = await LockAsync(FileMode.OpenOrCreate, LockMode.Auto);
                    }

                    LockInfo lockInfo = fileLock!=null ? await fileLock.GetLockInfoAsync() : null;

                    // Update item in remote storage.
                    logger.LogMessage("Sending to remote storage", userFileSystemPath);
                    await CreateOrUpdateAsync(FileMode.Open, lockInfo);
                    //await new UserFileSystemRawItem(userFileSystemPath).ClearStateAsync();
                    logger.LogMessage("Sent to remote storage succesefully", userFileSystemPath);
                }

                // Unlock if auto-locked.
                if (await Lock.GetLockModeAsync(userFileSystemPath) == LockMode.Auto)
                {
                    if (fileLock == null)
                    {
                        // Get existing lock.
                        fileLock = await Lock.LockAsync(userFileSystemPath, FileMode.Open, LockMode.None, logger);
                    }
                    await UnlockAsync(fileLock);
                }
            }
            catch(ClientLockFailedException ex)
            {
                // Failed to lock file. Possibly blocked from another thread. This is a normal behaviour.
                logger.LogMessage(ex.Message, userFileSystemPath);
            }
            catch (Exception ex)
            {
                // Some error when locking, updating or unlocking occured.
                await new UserFileSystemRawItem(userFileSystemPath).SetUploadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
            finally
            {
                if (fileLock != null)
                {
                    fileLock.Dispose();
                }
            }
        }

        /// <summary>
        /// Creates or updates the item in the remote storage.
        /// </summary>
        /// <param name="mode">
        /// Indicates if the file should created or updated. 
        /// Supported modes are <see cref="FileMode.CreateNew"/> and <see cref="FileMode.Open"/>
        /// </param>
        /// <param name="lockInfo">Information about the lock. Pass null if the item is not locked.</param>
        private async Task CreateOrUpdateAsync(FileMode mode, LockInfo lockInfo = null)
        {
            if ((mode != FileMode.CreateNew) && (mode != FileMode.Open))
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
                        // Opening a file for reading triggers hydration, make sure to open only if content is modified.
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
                            eTag = await new UserFile(userFileSystemPath, lockInfo).UpdateAsync((IFileBasicInfo)info, userFileSystemStream);
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
                            eTag = await new UserFolder(userFileSystemPath, lockInfo).UpdateAsync((IFolderBasicInfo)info);
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
                        logger.LogMessage("Moving item in remote storage", userFileSystemOldPath, userFileSystemNewPath);

                        // Read In-Sync state before move and set after move
                        if (FsPath.Exists(userFileSystemOldPath))
                        {
                            inSync = PlaceholderItem.GetItem(userFileSystemOldPath).GetInSync();
                        }

                        eTag = await ETag.GetETagAsync(userFileSystemOldPath);
                        ETag.DeleteETag(userFileSystemOldPath);

                        await new UserFileSystemItem(userFileSystemOldPath).MoveToAsync(userFileSystemNewPath);
                        updateTargetOnSuccess = true;
                        logger.LogMessage("Moved succesefully in remote storage", userFileSystemOldPath, userFileSystemNewPath);
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
                                await new UserFileSystemRawItem(userFileSystemNewPath).ClearStateAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string userFileSystemPath = FsPath.Exists(userFileSystemNewPath) ? userFileSystemNewPath : userFileSystemOldPath;
                await new UserFileSystemRawItem(userFileSystemPath).SetUploadErrorStateAsync(ex);

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

        /// <summary>
        /// Locks the file in the remote storage. 
        /// </summary>
        /// <param name="lockMode">
        /// Indicates automatic or manual lock.
        /// </param>
        /// <exception cref="ClientLockFailedException">
        /// Thrown when a file can not be locked. For example when a lock-token file is blocked 
        /// from another thread, during update, lock and unlock operations.
        /// </exception>
        internal async Task LockAsync(LockMode lockMode = LockMode.Manual)
        {
            using (await LockAsync(FileMode.CreateNew, lockMode))
            {
            }
        }

        /// <summary>
        /// Locks the file in the remote storage or gets existing lock. 
        /// </summary>
        /// <param name="lockFileOpenMode">
        /// Indicates if a new lock should be created or existing lock file to be opened.
        /// Allowed options are <see cref="FileMode.OpenOrCreate"/>, <see cref="FileMode.Open"/> and <see cref="FileMode.CreateNew"/>.
        /// </param>
        /// <param name="lockMode">
        /// Indicates automatic or manual lock. Saved only for new files, ignored when existing lock is opened.
        /// </param>
        /// <exception cref="ClientLockFailedException">
        /// Thrown when a file can not be locked. For example when a lock-token file is blocked 
        /// from another thread, during update, lock and unlock operations.
        /// </exception>
        /// <returns>File lock.</returns>
        private async Task<Lock> LockAsync(FileMode lockFileOpenMode, LockMode lockMode = LockMode.Manual)
        {
            // Get existing lock or create a new lock.
            Lock fileLock = await Lock.LockAsync(userFileSystemPath, lockFileOpenMode, lockMode, logger);

            if (fileLock.IsNew())
            {
                logger.LogMessage("Locking", userFileSystemPath);

                // Set pending icon.
                await new UserFileSystemRawItem(userFileSystemPath).SetLockPendingIconAsync(true);

                // Lock file in remote storage.
                LockInfo lockInfo = await new UserFileSystemItem(userFileSystemPath).LockAsync();

                // Save lock-token in lock-file.
                await fileLock.SetLockInfoAsync(lockInfo);

                // Set locked icon.
                await new UserFileSystemRawItem(userFileSystemPath).SetLockIconAsync(true);

                logger.LogMessage($"Locked succesefully. Mode: {lockMode}", userFileSystemPath);
            }

            return fileLock;
        }

        /// <summary>
        /// Unlocks the file in the remote storage.
        /// </summary>
        internal async Task UnlockAsync()
        {
            using (Lock fileLock = await LockAsync(FileMode.Open))
            {
                await UnlockAsync(fileLock);
            }
        }

        /// <summary>
        /// Unlocks the file in the remote storage using existing <see cref="Lock"/>.
        /// </summary>
        /// <param name="fileLock">File lock.</param>
        private async Task UnlockAsync(Lock fileLock)
        {
            logger.LogMessage("Unlocking", userFileSystemPath);

            // Set pending icon.
            await new UserFileSystemRawItem(userFileSystemPath).SetLockPendingIconAsync(true);

            // Read lock-token from lock-file.
            string lockToken = (await fileLock.GetLockInfoAsync()).LockToken;

            // Ulock file in remote storage.
            await new UserFileSystemItem(userFileSystemPath).UnlockAsync(lockToken);

            // Remove lock icon.
            await new UserFileSystemRawItem(userFileSystemPath).SetLockIconAsync(false);

            // Operation completed succesefully, delete lock files.
            fileLock.Unlock();

            logger.LogMessage("Unlocked succesefully", userFileSystemPath);
        }
    }
}