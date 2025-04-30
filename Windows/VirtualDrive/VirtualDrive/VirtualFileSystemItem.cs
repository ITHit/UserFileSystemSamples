using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;


namespace VirtualDrive
{
    ///<inheritdoc>
    public abstract class VirtualFileSystemItem : IFileSystemItemWindows, ILock
    {
        /// <summary>
        /// File or folder path in the user file system.
        /// </summary>
        protected readonly string UserFileSystemPath;

        /// <summary>
        /// File or folder item ID in the remote storage.
        /// </summary>
        protected readonly byte[] RemoteStorageItemId;

        /// <summary>
        /// Logger.
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Engine.
        /// </summary>
        protected readonly VirtualEngine Engine;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">File or folder path in the user file system.</param>
        /// <param name="remoteStorageItemId">Remote storage item ID.</param>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFileSystemItem(string userFileSystemPath, byte[] remoteStorageItemId, VirtualEngine engine, ILogger logger)
        {
            UserFileSystemPath = string.IsNullOrEmpty(userFileSystemPath) ? throw new ArgumentNullException(nameof(userFileSystemPath)) : userFileSystemPath;
            RemoteStorageItemId = remoteStorageItemId ?? throw new ArgumentNullException(nameof(remoteStorageItemId));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        ///<inheritdoc>
        public async Task MoveToAsync(string targetUserFileSystemPath, byte[] targetParentItemId, IOperationContext operationContext, IConfirmationResultContext resultContext, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewPath = targetUserFileSystemPath;
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogDebug($"{nameof(IFileSystemItem)}.{nameof(MoveToAsync)}()", userFileSystemOldPath, userFileSystemNewPath, operationContext);
        }

        /// <inheritdoc/>
        public async Task MoveToCompletionAsync(string targetUserFileSystemPath, byte[] targetFolderRemoteStorageItemId, IWindowsMoveContext operationContext, IInSyncStatusResultContext resultContext, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewPath = targetUserFileSystemPath;
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToCompletionAsync)}()", userFileSystemOldPath, userFileSystemNewPath, operationContext);

            if (!Mapping.TryGetRemoteStoragePathById(RemoteStorageItemId, out string remoteStorageOldPath)) return;
            if (!Mapping.TryGetRemoteStoragePathById(targetFolderRemoteStorageItemId, out string remoteStorageNewParentPath)) return;

            FileSystemInfo remoteStorageOldItem = FsPath.GetFileSystemItem(remoteStorageOldPath);

            if (remoteStorageOldItem != null)
            {
                string remoteStorageNewPath = Path.Combine(remoteStorageNewParentPath, Path.GetFileName(targetUserFileSystemPath));

                if (remoteStorageOldItem is FileInfo)
                {
                    if (File.Exists(remoteStorageNewPath))
                    {
                        File.Delete(remoteStorageNewPath);
                    }
                    (remoteStorageOldItem as FileInfo).MoveTo(remoteStorageNewPath);
                }
                else
                {
                    (remoteStorageOldItem as DirectoryInfo).MoveTo(remoteStorageNewPath);
                }
            }
        }

        ///<inheritdoc>
        public async Task DeleteAsync(IOperationContext operationContext, IConfirmationResultContext resultContext, CancellationToken cancellationToken = default)
        {
            Logger.LogDebug($"{nameof(IFileSystemItem)}.{nameof(DeleteAsync)}()", UserFileSystemPath, default, operationContext);

            // To cancel the operation and prevent the file from being deleted, 
            // call the resultContext.ReturnErrorResult() method or throw any exception inside this method:
            // resultContext.ReturnErrorResult(CloudFileStatus.STATUS_CLOUD_FILE_REQUEST_TIMEOUT);

            // IMPOTRTANT! See Windows Cloud API delete prevention bug description here: 
            // https://stackoverflow.com/questions/68887190/delete-in-cloud-files-api-stopped-working-on-windows-21h1
            // https://docs.microsoft.com/en-us/answers/questions/75240/bug-report-cfapi-ackdelete-borken-on-win10-2004.html

            // Note that some applications, such as Windows Explorer may call delete more than one time on the same file/folder.
        }

        /// <inheritdoc/>
        public async Task DeleteCompletionAsync(IOperationContext operationContext, IInSyncStatusResultContext resultContext, CancellationToken cancellationToken = default)
        {
            // On Windows, for rename with overwrite to function properly for folders, 
            // the deletion of the folder in the remote storage must be done in DeleteCompletionAsync()
            // Otherwise the source folder will be deleted before files in it can be moved.

            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteCompletionAsync)}()", UserFileSystemPath, default, operationContext);

            if (!Mapping.TryGetRemoteStoragePathById(RemoteStorageItemId, out string remoteStoragePath)) return;

            try
            {
                FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(remoteStoragePath);
                if (remoteStorageItem != null)
                {
                    if (remoteStorageItem is FileInfo)
                    {
                        remoteStorageItem.Delete();
                    }
                    else
                    {
                        (remoteStorageItem as DirectoryInfo).Delete(true);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                resultContext.SetInSync = false; // We want the Engine to try deleting this file again at a later time.
                Logger.LogError("Failed to delete item", UserFileSystemPath, default, null, operationContext);
            }
            catch (DirectoryNotFoundException)
            {
                // Windows Explorer may call delete more than one time on the same file/folder.
                Logger.LogDebug("Folder already deleted", UserFileSystemPath, default, operationContext);
            }
            catch (FileNotFoundException)
            {
                // Windows Explorer may call delete more than one time on the same file/folder.
                Logger.LogDebug("File already deleted", UserFileSystemPath, default, operationContext);
            }
        }

        /// <inheritdoc/>
        public async Task<byte[]> GetThumbnailAsync(uint size, IOperationContext operationContext)
        {
            // For this method to be called you need to register a thumbnail handler.
            // See method description for more details.

            if (!Mapping.TryGetRemoteStoragePathById(RemoteStorageItemId, out string remoteStoragePath)) return null;

            byte[] thumbnail = ThumbnailExtractor.GetRemoteThumbnail(remoteStoragePath, size);

            string thumbnailResult = thumbnail != null ? "Success" : "Not Impl";
            Logger.LogDebug($"{nameof(IFileSystemItem)}.{nameof(GetThumbnailAsync)}(): {thumbnailResult}", UserFileSystemPath, default, operationContext);

            return thumbnail;
        }

        
        /// <inheritdoc/>
        public async Task<IEnumerable<FileSystemItemPropertyData>> GetPropertiesAsync(IOperationContext operationContext)
        {
            // For this method to be called you need to register a properties handler.
            // See method description for more details.

            Logger.LogDebug($"{nameof(IFileSystemItem)}.{nameof(GetPropertiesAsync)}()", UserFileSystemPath, default, operationContext);

            IList<FileSystemItemPropertyData> props = new List<FileSystemItemPropertyData>();

            // Read LockInfo and choose the lock icon.
            if (operationContext.Properties.TryGetActiveLockInfo(out ServerLockInfo lockInfo))
            {
                // Determine if the item is locked by this user or thirt-party user.
                bool thisUser = Engine.IsCurrentUser(lockInfo.Owner);
                string lockIconName = thisUser ? "Locked" : "LockedByAnotherUser";

                // Get Lock Mode.
                if (thisUser && (lockInfo.Mode == LockMode.Auto))
                {
                    lockIconName += "Auto";
                }

                // Set Lock Owner.
                FileSystemItemPropertyData propertyLockOwner = new FileSystemItemPropertyData()
                {
                    Id = (int)CustomColumnIds.LockOwnerIcon,
                    Value = lockInfo.Owner,
                    IconResource = Path.Combine(Engine.IconsFolderPath, lockIconName + ".ico")
                };
                props.Add(propertyLockOwner);

                // Set Lock Expires.
                FileSystemItemPropertyData propertyLockExpires = new FileSystemItemPropertyData()
                {
                    Id = (int)CustomColumnIds.LockExpirationDate,
                    Value = lockInfo.LockExpirationDateUtc.ToString(),
                    IconResource = Path.Combine(Engine.IconsFolderPath, "Empty.ico")
                };
                props.Add(propertyLockExpires);

                // Set Lock Scope
                FileSystemItemPropertyData propertyLockScope = new FileSystemItemPropertyData()
                {
                    Id = (int)CustomColumnIds.LockScope,
                    Value = lockInfo.Exclusive ? "Exclusive" : "Shared",
                    IconResource = Path.Combine(Engine.IconsFolderPath, "Empty.ico")
                };
                props.Add(propertyLockScope);
            }

            return props;
        }
        

        ///<inheritdoc>
        public async Task LockAsync(LockMode lockMode, IOperationContext operationContext, CancellationToken cancellationToken)
        {
            Logger.LogMessage($"{nameof(ILock)}.{nameof(LockAsync)}()", UserFileSystemPath, default, operationContext);

            // Call your remote storage here to lock the item.
            // Save the lock token and other lock info received from your remote storage on the client.
            // Supply the lock-token as part of each remote storage update in File.WriteAsync() method.
            // For demo purposes we just fill some generic data.
            ServerLockInfo serverLockInfo = new ServerLockInfo()
            {
                LockToken = "ServerToken",
                Owner = Engine.CurrentUserPrincipal,
                Exclusive = true,
                LockExpirationDateUtc = DateTimeOffset.Now.AddMinutes(30),
                Mode = lockMode
            };

            // Save lock-token and lock-mode.
            operationContext.Properties.SetLockInfo(serverLockInfo);


            if (lockMode == LockMode.Auto)
            {
                // Start the timer to unlock if the file is not locked.
                System.Timers.Timer timer = new System.Timers.Timer(10000);
                timer.AutoReset = true;
                timer.Elapsed += async (object sender, System.Timers.ElapsedEventArgs e) =>
                {
                    try
                    {                        
                        if (cancellationToken.IsCancellationRequested) return;

                        if (operationContext.Properties.TryGetActiveLockInfo(out ServerLockInfo serverLockInfo) &&
                            FilterHelper.IsLockedWithOwnerFile(UserFileSystemPath) && !FilterHelper.IsOwnerFileExists(UserFileSystemPath))
                        {
                            await Engine.ClientNotifications(UserFileSystemPath).UnlockAsync(true, cancellationToken);
                            PlaceholderItem.UpdateUI(UserFileSystemPath);
                            // Stop the timer.
                            timer.Dispose();
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // Stop the timer.
                        timer.Dispose();

                        Logger.LogDebug("Checking lock canceled", UserFileSystemPath, default, operationContext);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Checking lock failed", UserFileSystemPath, default, ex, operationContext);
                    }
                };
                timer.Start();
            }
        }

        ///<inheritdoc>
        public async Task<LockMode> GetLockModeAsync(IOperationContext operationContext, CancellationToken cancellationToken)
        {
            if (operationContext.Properties.TryGetActiveLockInfo(out ServerLockInfo lockInfo))
            {
                return lockInfo.Mode;
            }

            return LockMode.None;
        }

        ///<inheritdoc>
        public async Task UnlockAsync(IOperationContext operationContext, CancellationToken cancellationToken)
        {
            Logger.LogMessage($"{nameof(ILock)}.{nameof(UnlockAsync)}()", UserFileSystemPath, default, operationContext);

            // Read the lock-token.
            if (operationContext.Properties.TryGetActiveLockInfo(out ServerLockInfo lockInfo))
            {
                // Unlock the item in the remote storage here.
            }
            // Delete lock-mode and lock-token info.
            operationContext.Properties.TryDeleteLockInfo();
        }

        public Task<IMetadata> GetMetadataAsync(IOperationContext operationContext, IResultContextBase resultContext, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
