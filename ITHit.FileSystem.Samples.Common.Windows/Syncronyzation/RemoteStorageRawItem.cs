using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;

namespace ITHit.FileSystem.Samples.Common.Windows.Syncronyzation
{
    /// <summary>
    /// Provides methods for synching the user file system to the remote storage.
    /// Creates, updates, deletes, moves, locks and unloacks files and folders based on the info from user file system.
    /// This class also sets status icons in file manager.
    /// </summary>
    /// <remarks>In most cases you can use this class in your project without any changes.</remarks>
    public class RemoteStorageRawItem<TItemType> : IRemoteStorageRawItem where TItemType : IVirtualFileSystemItem
    {
        /// <summary>
        /// Path to the file or folder in the user file system.
        /// </summary>
        private readonly string userFileSystemPath;

        /// <summary>
        /// Virtual drive.
        /// </summary>
        private readonly VirtualDriveBase virtualDrive;

        /// <summary>
        /// Logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Manages loc-sync, lock-info and lock-mode files.
        /// </summary>
        private readonly LockManager lockManager;

        /// <summary>
        /// File system item that corresponds to  <see cref="userFileSystemPath"/>.
        /// </summary>
        private TItemType item;

        private readonly UserFileSystemRawItem userFileSystemRawItem;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">File or folder path in the user file system.</param>
        /// <param name="logger">Logger.</param>
        internal RemoteStorageRawItem(string userFileSystemPath, VirtualDriveBase virtualDrive, ILogger logger)
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
            this.lockManager = virtualDrive.LockManager(userFileSystemPath, logger);
            this.userFileSystemRawItem = virtualDrive.GetUserFileSystemRawItem(userFileSystemPath, logger);
        }

        /*
        /// <summary>
        /// Creates a new file or folder in the remote storage.
        /// </summary>
        /// <param name="userFileSystemNewItemPath">Path to the file or folder in the user file system to be created in the remote storage.</param>
        /// <param name="logger">Logger</param>
        internal static async Task CreateAsync<IItemTypeCreate>(string userFileSystemNewItemPath, VirtualDriveBase userEngine, ILogger logger) where IItemTypeCreate : IUserFileSystemItem
        {
            await new RemoteStorageRawItem<IItemTypeCreate>(userFileSystemNewItemPath, userEngine, logger).CreateAsync();
        }
        */

        /// <summary>
        /// This method reduces the number of 
        /// <see cref="IVirtualDrive.GetVirtualFileSystemItemAsync(string, FileSystemItemTypesEnum)"/> calls.
        /// </summary>
        /// <param name="userFileSystemPath">Path of the item in the user file system.</param>
        /// <returns>File or folder that corresponds to path.</returns>
        private async Task<TItemType> GetItemAsync(string userFileSystemPath)
        {
            if (item == null)
            {
                item = await virtualDrive.GetItemAsync<TItemType>(userFileSystemPath, logger);
                if (item == null)
                {
                    throw new FileNotFoundException(userFileSystemPath);
                }
            }
            return item;
        }

        /// <summary>
        /// Returns true if the item implements <see cref="IVirtualLock"/>, false - otherwise.
        /// </summary>
        internal async Task<bool> IsLockSupportedAsync()
        {
            return await GetItemAsync(userFileSystemPath) is IVirtualLock;
        }

        /// <inheritdoc/>
        public async Task CreateAsync()
        {
            try
            {
                logger.LogMessage("Creating item in the remote storage", userFileSystemPath);
                await CreateOrUpdateAsync(FileMode.CreateNew);
                await userFileSystemRawItem.ClearStateAsync();
                logger.LogMessage("Created in the remote storage succesefully", userFileSystemPath);
            }
            catch (Exception ex)
            {
                await userFileSystemRawItem.SetUploadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }

        /// <inheritdoc/>
        public async Task UpdateAsync()
        {
            // This protects from ocationally calling update on moved items.
            if (PlaceholderItem.GetItem(userFileSystemPath).IsMoved())
            {
                string originalPath = PlaceholderItem.GetItem(userFileSystemPath).GetOriginalPath();
                throw new ConflictException(Modified.Client, $"The item was moved. Original path: {originalPath}");
            }
            
            LockSync lockSync = null;
            try
            {
                if (!PlaceholderItem.GetItem(userFileSystemPath).GetInSync())
                {
                    // This will make sure only one thread can do the lock-update-unlock sequence.
                    lockSync = await lockManager.LockAsync();

                    IVirtualFileSystemItem userFileSystemItem = await GetItemAsync(userFileSystemPath);

                    ServerLockInfo lockInfo = null;
                    if (userFileSystemItem is IVirtualLock)
                    {
                        // Get lock info in case of manual lock or failed lock/update/unlock
                        if (await lockManager.IsLockedAsync())
                        {
                            lockInfo = await lockManager.GetLockInfoAsync();
                        } 

                        // Lock file if auto-locking is enabled.
                        else if (virtualDrive.Settings.AutoLock)
                        {
                            // Lock file in remote storage.
                            lockInfo = await LockAsync(lockSync, LockMode.Auto);
                        }
                    }

                    // Update item in remote storage.
                    logger.LogMessage("Sending to remote storage", userFileSystemPath);
                    await CreateOrUpdateAsync(FileMode.Open, lockInfo);
                    //await new UserFileSystemRawItem(userFileSystemPath).ClearStateAsync();
                    logger.LogMessage("Sent to remote storage succesefully", userFileSystemPath);
                }

                // Unlock if auto-locked.
                if (await lockManager.GetLockModeAsync() == LockMode.Auto)
                {
                    // Required in case the file failed to unlock during previous update.
                    lockSync ??= await lockManager.LockAsync();

                    // Send unlock request to remote storage.
                    await UnlockAsync(lockSync);
                }
            }
            catch (ClientLockFailedException ex)
            {
                // Failed to lock file. Possibly blocked from another thread. This is a normal behaviour.
                logger.LogMessage(ex.Message, userFileSystemPath);
            }
            catch (Exception ex)
            {
                // Some error when locking, updating or unlocking occured.
                await userFileSystemRawItem.SetUploadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
            finally
            {
                if (lockSync != null)
                {
                    lockSync.Dispose();
                }
                // Do not delete lock-token and lock-mode files here, it is required if IUserLock 
                // interfce is implemented and the file was locked manually. 
            }
        }

        /// <summary>
        /// Creates or updates the item in the remote storage.
        /// </summary>
        /// <param name="mode">
        /// Indicates if the file should created or updated. 
        /// Supported modes are <see cref="FileMode.CreateNew"/> and <see cref="FileMode.Open"/>
        /// </param>
        /// <param name="lockInfo">
        /// Information about the lock. Pass null if the item is not locked.
        /// </param>
        private async Task CreateOrUpdateAsync(FileMode mode, ServerLockInfo lockInfo = null)
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

                IFileSystemItemMetadata info = GetMetadata(userFileSystemItem);

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
                            IVirtualFolder userFolder = await virtualDrive.GetItemAsync<IVirtualFolder>(userFileSystemParentPath, logger);
                            eTag = await userFolder.CreateFileAsync((IFileMetadata)info, userFileSystemStream);
                        }
                        else
                        {
                            IVirtualFile userFile = await GetItemAsync(userFileSystemPath) as IVirtualFile;
                            eTag = await virtualDrive.GetETagManager(userFileSystemPath).GetETagAsync();
                            eTag = await userFile.UpdateAsync((IFileMetadata)info, userFileSystemStream, eTag, lockInfo);
                        }
                    }
                    else
                    {
                        if (mode == FileMode.CreateNew)
                        {
                            string userFileSystemParentPath = Path.GetDirectoryName(userFileSystemPath);
                            IVirtualFolder userFolder = await virtualDrive.GetItemAsync<IVirtualFolder>(userFileSystemParentPath, logger);
                            eTag = await userFolder.CreateFolderAsync((IFolderMetadata)info);
                        }
                        else
                        {
                            IVirtualFolder userFolder = await GetItemAsync(userFileSystemPath) as IVirtualFolder;
                            eTag = await virtualDrive.GetETagManager(userFileSystemPath).GetETagAsync();
                            eTag = await userFolder.UpdateAsync((IFolderMetadata)info, eTag, lockInfo);
                        }
                    }
                    await virtualDrive.GetETagManager(userFileSystemPath).SetETagAsync(eTag);
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
        /// <returns>Object implementing <see cref="IFileSystemItemMetadata"/> interface.</returns>
        private static IFileSystemItemMetadata GetMetadata(FileSystemInfo userFileSystemItem)
        {
            FileSystemItemMetadata itemInfo;

            if (userFileSystemItem is FileInfo)
            {
                itemInfo = new FileMetadata();
            }
            else
            {
                itemInfo = new FolderMetadata();
            }

            // Remove Pinned, unpinned and offline flags,
            // so they are not sent to the remote storage by mistake.
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
                ((FileMetadata)itemInfo).Length = ((FileInfo)userFileSystemItem).Length;
            };

            return itemInfo;
        }

        /// <inheritdoc/>
        public async Task MoveToAsync(string userFileSystemNewPath)
        {
            try
            {
                await MoveToAsync(userFileSystemNewPath, null);
            }
            finally
            {
                await new RemoteStorageRawItem<TItemType>(userFileSystemNewPath, virtualDrive, logger).MoveToCompletionAsync();
            }
        }

        /// <summary>
        /// Moves the item in the remote storage. This method is called by the platform. 
        /// To move item manually use the <see cref="MoveToAsync(string)"/> method instead.
        /// </summary>
        /// <param name="userFileSystemNewPath">Target path in user file system.</param>
        /// <param name="resultContext">Confirms move competeion. Passed by the platform only.</param>
        internal async Task MoveToAsync(string userFileSystemNewPath, IConfirmationResultContext resultContext)
        {
            string userFileSystemOldPath = userFileSystemPath;

            try
            {
                //bool? inSync = null;
                //bool updateTargetOnSuccess = false;
                //string eTag = null;
                try
                {
                    if (!FsPath.IsRecycleBin(userFileSystemNewPath) // When a file is deleted, it is moved to a Recycle Bin.
                        && !FsPath.AvoidSync(userFileSystemOldPath) && !FsPath.AvoidSync(userFileSystemNewPath))
                    {
                        logger.LogMessage("Moving item in remote storage", userFileSystemOldPath, userFileSystemNewPath);
                        /*
                        // Read In-Sync state before move and set after move.
                        if (FsPath.Exists(userFileSystemOldPath))
                        {
                            inSync = PlaceholderItem.GetItem(userFileSystemOldPath).GetInSync();
                        }
                        */

                        IVirtualFileSystemItem userFileSystemItemOld = await GetItemAsync(userFileSystemOldPath);
                        await userFileSystemItemOld.MoveToAsync(userFileSystemNewPath);
                        //updateTargetOnSuccess = true;
                        logger.LogMessage("Moved succesefully in remote storage", userFileSystemOldPath, userFileSystemNewPath);

                        ETagManager eTagManager = virtualDrive.GetETagManager(userFileSystemOldPath);
                        if (FsPath.Exists(eTagManager.ETagFilePath))
                        {
                            await eTagManager.MoveToAsync(userFileSystemNewPath);
                            logger.LogMessage("Moved ETag succesefully", userFileSystemOldPath, userFileSystemNewPath);
                        }
                    }
                }
                finally
                {
                    if (resultContext != null)
                    {
                        // Calling ReturnConfirmationResult() moves file in the user file system.
                        logger.LogMessage("Confirming move in user file system", userFileSystemOldPath, userFileSystemNewPath);
                        resultContext.ReturnConfirmationResult();
                        // After this the MoveToCompletionAsync() method is called. 
                        // After that, in case of a file, the file handle is closed, triggering IFile.CloseAsync() call 
                        // and Windows File Manager move progress window is closed. 
                        // In case the target is the offline folder, the IFolder.GetChildrenAsync() method is called.
                    }
                }
            }
            catch (Exception ex)
            {
                string userFileSystemExPath = FsPath.Exists(userFileSystemNewPath) ? userFileSystemNewPath : userFileSystemOldPath;
                await virtualDrive.GetUserFileSystemRawItem(userFileSystemExPath, logger).SetUploadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }

        /// <summary>
        /// Completes move. This method is called by the platform. 
        /// To move item manually use the <see cref="MoveToAsync(string)"/> method instead.
        /// Sets In-Sync state based on file content changes,
        /// Updates OriginalPath so the file does not appear as moved.
        /// </summary>
        /// <returns></returns>
        internal async Task MoveToCompletionAsync()
        {
            string userFileSystemNewPath = userFileSystemPath;

            if (FsPath.Exists(userFileSystemNewPath)            // This check is just to avoid extra error in the log.
                && !FsPath.IsRecycleBin(userFileSystemNewPath)  // When a file with content is deleted, it is moved to a Recycle Bin.
                && !FsPath.AvoidAutoLock(userFileSystemNewPath))// No need to update temp MS Office docs.
            {
                // Open file to prevent reads and changes between GetFileDataSizeInfo() call and SetInSync() call.
                //using (WindowsFileSystemItem userFileSystemWinItem = WindowsFileSystemItem.OpenReadAttributes(userFileSystemNewPath, FileMode.Open, FileShare.None))
                {
                    // If a file with content is deleted it is moved to a recycle bin and converted
                    // to a regular file, so placeholder features are not available on it, checking if a file is a placeholder.
                    if (/*updateTargetOnSuccess &&*/ PlaceholderItem.IsPlaceholder(userFileSystemNewPath))
                    {
                        PlaceholderItem placeholderNew = PlaceholderItem.GetItem(userFileSystemNewPath);
                        // Restore In-sync state.
                        /*
                        if (inSync != null)
                        {
                            placeholderNew.SetInSync(inSync.Value);
                        }
                        else 
                        */
                        if (((placeholderNew is PlaceholderFile) && ((PlaceholderFile)placeholderNew).GetFileDataSizeInfo().ModifiedDataSize == 0)
                            || (placeholderNew is PlaceholderFolder))
                        {
                            logger.LogMessage("Setting In-Sync state", userFileSystemNewPath);
                            placeholderNew.SetInSync(true);
                        }
                    }

                }

                // Recursively restore OriginalPath and the 'locked' icon.
                await MoveToCompletionRecursiveAsync(userFileSystemNewPath);
            }
        }

        /// <summary>
        /// Recursively restores OriginalPath and the 'locked' icon.
        /// </summary>
        /// <param name="userFileSystemNewPath">Path in the user file system to start recursive processing.</param>
        private async Task MoveToCompletionRecursiveAsync(string userFileSystemNewPath)
        {
            if (FsPath.IsFolder(userFileSystemNewPath))
            {
                if (!new DirectoryInfo(userFileSystemNewPath).Attributes.HasFlag(System.IO.FileAttributes.Offline))
                {
                    //LogMessage("Folder offline, skipping:", userFileSystemFolderPath);

                    IEnumerable<string> userFileSystemChildren = Directory.EnumerateFileSystemEntries(userFileSystemNewPath, "*");

                    foreach (string userFileSystemChildPath in userFileSystemChildren)
                    {
                        try
                        {
                            await MoveToCompletionRecursiveAsync(userFileSystemChildPath);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError("Failed to complete move", userFileSystemChildPath, null, ex);
                        }
                    }
                }
            }

            if (FsPath.Exists(userFileSystemNewPath)            // This check is just to avoid extra error in the log.
                && !FsPath.IsRecycleBin(userFileSystemNewPath)  // When a file with content is deleted, it is moved to a Recycle Bin.
                && !FsPath.AvoidAutoLock(userFileSystemNewPath))// No need to update temp MS Office docs.
            {
                // Open file to prevent reads and changes between GetFileDataSizeInfo() call and SetInSync() call.
                using (WindowsFileSystemItem userFileSystemWinItem = WindowsFileSystemItem.OpenReadAttributes(userFileSystemNewPath, FileMode.Open, FileShare.None))
                {
                    // If a file with content is deleted it is moved to a recycle bin and converted
                    // to a regular file, so placeholder features are not available on it, checking if a file is a placeholder.
                    if (/*updateTargetOnSuccess &&*/ PlaceholderItem.IsPlaceholder(userFileSystemNewPath))
                    {
                        PlaceholderItem placeholderNew = PlaceholderItem.GetItem(userFileSystemNewPath);

                        await virtualDrive.GetUserFileSystemRawItem(userFileSystemNewPath, logger).ClearStateAsync();

                        // Update OriginalPath if the item is not new.
                        // Required for pptx and xlsx files Save As operation.
                        if (!placeholderNew.IsNew(virtualDrive))
                        {
                            // Update OriginalPath, so the item does not appear as moved.
                            logger.LogMessage("Setting Original Path", userFileSystemNewPath);
                            placeholderNew.SetOriginalPath(userFileSystemNewPath);
                        }

                        // Restore the 'locked' icon.
                        ServerLockInfo existingLock = await virtualDrive.LockManager(userFileSystemNewPath, logger).GetLockInfoAsync();
                        await virtualDrive.GetUserFileSystemRawItem(userFileSystemNewPath, logger).SetLockInfoAsync(existingLock);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync()
        {
            if (!FsPath.AvoidSync(userFileSystemPath))
            {
                IVirtualFileSystemItem userFileSystemItem = await GetItemAsync(userFileSystemPath);
                await userFileSystemItem.DeleteAsync();

                virtualDrive.GetETagManager(userFileSystemPath).DeleteETag();

                return true;
            }

            return false;
        }

        /// <summary>
        /// Locks the item in the remote storage. The item must implement <see cref="IVirtualLock"/> interface. 
        /// </summary>
        /// <param name="lockMode">Indicates automatic or manual lock.</param>
        /// <exception cref="ClientLockFailedException">
        /// Thrown when a file can not be locked. For example when a lock-token file is blocked 
        /// from another thread, during update, lock and unlock operations.
        /// </exception>
        public async Task LockAsync(LockMode lockMode = LockMode.Manual)
        {
            // Verify that the item supports locking.
            if(!(await GetItemAsync(userFileSystemPath) is IVirtualLock))
            {
                throw new NotSupportedException(nameof(IVirtualLock));
            }

            using (LockSync lockSync = await lockManager.LockAsync())
            {
                await LockAsync(lockSync, lockMode);
            }
        }

        /// <summary>
        /// Locks the item in the remote storage. The item must implement <see cref="IVirtualLock"/> interface.
        /// </summary>
        /// <param name="lockSync">Sync lock.</param>
        /// <param name="lockMode">Indicates automatic or manual lock.</param>
        private async Task<ServerLockInfo> LockAsync(LockSync lockSync, LockMode lockMode)
        {
            ServerLockInfo lockInfo = null;
            try
            {
                logger.LogMessage("Locking in remote storage", userFileSystemPath);

                // Set lock-pending icon.
                await userFileSystemRawItem.SetLockPendingIconAsync(true);

                // Lock file in remote storage.
                IVirtualLock userLock = await GetItemAsync(userFileSystemPath) as IVirtualLock;
                lockInfo = await userLock.LockAsync();

                // Save lock-token and lock-mode.
                await lockManager.SetLockInfoAsync(lockInfo);
                await lockManager.SetLockModeAsync(lockMode);

                logger.LogMessage("Locked in remote storage succesefully.", userFileSystemPath);
            }
            catch (Exception ex)
            {
                logger.LogError("Locking in remote storage failed.", userFileSystemPath, null, ex);

                // Delete lock-token and lock-mode files.
                await lockManager.DeleteLockAsync();

                // Clear lock icon.
                await userFileSystemRawItem.SetLockInfoAsync(null);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }

            // Set locked icon and all lock properties.
            await userFileSystemRawItem.SetLockInfoAsync(lockInfo);
            return lockInfo;
        }

        /// <summary>
        /// Unlocks the item in the remote storage. The item must implement <see cref="IVirtualLock"/> interface. 
        /// </summary>
        /// <exception cref="ClientLockFailedException">
        /// Thrown when a file can not be locked. For example when a lock-token file is blocked 
        /// from another thread, during update, lock and unlock operations.
        /// </exception>
        public async Task UnlockAsync()
        {
            // Verify that the item supports locking.
            if (!(await GetItemAsync(userFileSystemPath) is IVirtualLock))
            {
                throw new NotSupportedException(nameof(IVirtualLock));
            }

            using (LockSync lockSync = await lockManager.LockAsync())
            {
                await UnlockAsync(lockSync);
            }
        }

        /// <summary>
        /// Unlocks the file in the remote storage using existing <see cref="LockSync"/>.
        /// </summary>
        /// <param name="lockSync">Sync lock.</param>
        private async Task UnlockAsync(LockSync lockSync)
        {
            logger.LogMessage("Unlocking in remote storage", userFileSystemPath);

            // Set pending icon.
            await userFileSystemRawItem.SetLockPendingIconAsync(true);

            // Read lock-token from lock-info file.
            string lockToken = (await lockManager.GetLockInfoAsync()).LockToken;

            // Unlock file in remote storage.
            IVirtualLock userLock = await GetItemAsync(userFileSystemPath) as IVirtualLock;
            await userLock.UnlockAsync(lockToken);

            // Delete lock-mode and lock-token files.
            await lockManager.DeleteLockAsync();

            // Remove lock icon.
            await userFileSystemRawItem.SetLockInfoAsync(null);

            logger.LogMessage("Unlocked in remote storage succesefully", userFileSystemPath);
        }
    }
}