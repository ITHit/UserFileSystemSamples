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
    public abstract class VirtualFileSystemItem : IFileSystemItem, ILock
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
        public async Task MoveToAsync(string targetUserFileSystemPath, byte[] targetParentItemId, IOperationContext operationContext = null, IConfirmationResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewPath = targetUserFileSystemPath;
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToAsync)}()", userFileSystemOldPath, userFileSystemNewPath, operationContext);
        }

        /// <inheritdoc/>
        public async Task MoveToCompletionAsync(string targetUserFileSystemPath, byte[] targetFolderRemoteStorageItemId, IMoveCompletionContext operationContext = null, IInSyncStatusResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewPath = targetUserFileSystemPath;
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToCompletionAsync)}()", userFileSystemOldPath, userFileSystemNewPath, operationContext);

            string remoteStorageOldPath = Mapping.GetRemoteStoragePathById(RemoteStorageItemId);
            FileSystemInfo remoteStorageOldItem = FsPath.GetFileSystemItem(remoteStorageOldPath);

            if (remoteStorageOldItem != null)
            {
                string remoteStorageNewParentPath = WindowsFileSystemItem.GetPathByItemId(targetFolderRemoteStorageItemId);
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

                // As soon as in this sample we use USN as a ETag, and USN chandes on move,
                // we need to update it for hydrated files.
                //await Engine.Mapping.UpdateETagAsync(remoteStorageNewPath, userFileSystemNewPath);

                Logger.LogMessage("Moved in the remote storage succesefully", userFileSystemOldPath, targetUserFileSystemPath, operationContext);
            }
        }

        ///<inheritdoc>
        public async Task DeleteAsync(IOperationContext operationContext, IConfirmationResultContext resultContext, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteAsync)}()", UserFileSystemPath, default, operationContext);

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
            // On Windows, for move with overwrite on folders to function correctly, 
            // the deletion of the folder in the remote storage must be done in DeleteCompletionAsync()
            // Otherwise the folder will be deleted before files in it can be moved.

            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteCompletionAsync)}()", UserFileSystemPath, default, operationContext);

            string remoteStoragePath;
            try
            {
                remoteStoragePath = Mapping.GetRemoteStoragePathById(RemoteStorageItemId);

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
                    Logger.LogMessage("Deleted in the remote storage succesefully", UserFileSystemPath, default, operationContext);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // We want the Engine to try deleting this file again at a later time.
                resultContext.SetInSync = false;
                Logger.LogError("Failed to delete item", UserFileSystemPath, default, null, operationContext);
            }
            catch (DirectoryNotFoundException)
            {
                // Windows Explorer may call delete more than one time on the same file/folder.
                Logger.LogMessage("Folder already deleted", UserFileSystemPath, default, operationContext);
            }
            catch (FileNotFoundException)
            {
                // Windows Explorer may call delete more than one time on the same file/folder.
                Logger.LogMessage("File already deleted", UserFileSystemPath, default, operationContext);
            }
        }

        /// <inheritdoc/>
        public async Task<byte[]> GetThumbnailAsync(uint size)
        {
            // For this method to be called you need to register a thumbnail handler.
            // See method description for more details.

            string remoteStorageItemPath = Mapping.GetRemoteStoragePathById(RemoteStorageItemId);
            byte[] thumbnail = ThumbnailExtractor.GetRemoteThumbnail(remoteStorageItemPath, size);

            string thumbnailResult = thumbnail != null ? "Success" : "Not Impl";
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(GetThumbnailAsync)}() - {thumbnailResult}", UserFileSystemPath);

            return thumbnail;
        }

        
        /// <inheritdoc/>
        public async Task<IEnumerable<FileSystemItemPropertyData>> GetPropertiesAsync()
        {
            // For this method to be called you need to register a properties handler.
            // See method description for more details.

            Logger.LogDebug($"{nameof(IFileSystemItem)}.{nameof(GetPropertiesAsync)}()", UserFileSystemPath);

            IList<FileSystemItemPropertyData> props = new List<FileSystemItemPropertyData>();

            if (Engine.Placeholders.TryGetItem(UserFileSystemPath, out PlaceholderItem placeholder))
            {

                // Read LockInfo.
                if (placeholder.Properties.TryGetValue("LockInfo", out IDataItem propLockInfo))
                {
                    if (propLockInfo.TryGetValue<ServerLockInfo>(out ServerLockInfo lockInfo))
                    {
                        // Get Lock Owner.
                        FileSystemItemPropertyData propertyLockOwner = new FileSystemItemPropertyData()
                        {
                            Id = (int)CustomColumnIds.LockOwnerIcon,
                            Value = lockInfo.Owner,
                            IconResource = System.IO.Path.Combine(Engine.IconsFolderPath, "Locked.ico")
                        };
                        props.Add(propertyLockOwner);

                        // Get Lock Expires.
                        FileSystemItemPropertyData propertyLockExpires = new FileSystemItemPropertyData()
                        {
                            Id = (int)CustomColumnIds.LockExpirationDate,
                            Value = lockInfo.LockExpirationDateUtc.ToString(),
                            IconResource = System.IO.Path.Combine(Engine.IconsFolderPath, "Empty.ico")
                        };
                        props.Add(propertyLockExpires);
                    }
                }

                // Read LockMode.
                if (placeholder.Properties.TryGetValue("LockMode", out IDataItem propLockMode))
                {
                    if (propLockMode.TryGetValue<LockMode>(out LockMode lockMode) && lockMode != LockMode.None)
                    {
                        FileSystemItemPropertyData propertyLockMode = new FileSystemItemPropertyData()
                        {
                            Id = (int)CustomColumnIds.LockScope,
                            Value = "Locked",
                            IconResource = System.IO.Path.Combine(Engine.IconsFolderPath, "Empty.ico")
                        };
                        props.Add(propertyLockMode);
                    }
                }

                // Read ETag.
                if (placeholder.Properties.TryGetValue("ETag", out IDataItem propETag))
                {
                    if (propETag.TryGetValue<string>(out string eTag))
                    {
                        FileSystemItemPropertyData propertyETag = new FileSystemItemPropertyData()
                        {
                            Id = (int)CustomColumnIds.ETag,
                            Value = eTag,
                            IconResource = System.IO.Path.Combine(Engine.IconsFolderPath, "Empty.ico")
                        };
                        props.Add(propertyETag);
                    }
                }
            }

            return props;
        }
        

        ///<inheritdoc>
        public Task<IFileSystemItemMetadata> GetMetadataAsync()
        {
            // Return IFileMetadata for a file, IFolderMetadata for a folder.
            throw new NotImplementedException();
        }

        ///<inheritdoc>
        public async Task LockAsync(LockMode lockMode, IOperationContext operationContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(ILock)}.{nameof(LockAsync)}()", UserFileSystemPath, default, operationContext);

            // Call your remote storage here to lock the item.
            // Save the lock token and other lock info received from your remote storage on the client.
            // Supply the lock-token as part of each remote storage update in File.WriteAsync() method.
            // For demo purposes we just fill some generic data.
            ServerLockInfo serverLockInfo = new ServerLockInfo() 
            { 
                LockToken = "ServerToken", 
                Owner = "You", 
                Exclusive = true, 
                LockExpirationDateUtc = DateTimeOffset.Now.AddMinutes(30) 
            };

            // Save lock-token and lock-mode.
            PlaceholderItem placeholder = Engine.Placeholders.GetItem(UserFileSystemPath);
            await placeholder.Properties.AddOrUpdateAsync("LockInfo", serverLockInfo);
            await placeholder.Properties.AddOrUpdateAsync("LockMode", lockMode);

            Logger.LogMessage("Locked in the remote storage succesefully", UserFileSystemPath, default, operationContext);
        }

        ///<inheritdoc>
        public async Task<LockMode> GetLockModeAsync(IOperationContext operationContext = null, CancellationToken cancellationToken = default)
        {
            if (Engine.Placeholders.TryGetItem(UserFileSystemPath, out PlaceholderItem placeholder))
            {
                if (placeholder.Properties.TryGetValue("LockMode", out IDataItem property))
                {
                    return await property.GetValueAsync<LockMode>();
                }
            }

            return LockMode.None;
        }

        ///<inheritdoc>
        public async Task UnlockAsync(IOperationContext operationContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(ILock)}.{nameof(UnlockAsync)}()", UserFileSystemPath, default, operationContext);
            
            if (Engine.Placeholders.TryGetItem(UserFileSystemPath, out PlaceholderItem placeholder))
            {
                // Read the lock-token.
                string lockToken = (await placeholder.Properties["LockInfo"].GetValueAsync<ServerLockInfo>())?.LockToken;

                // Unlock the item in the remote storage here.

                // Delete lock-mode and lock-token info.
                placeholder.Properties.Remove("LockInfo");
                placeholder.Properties.Remove("LockMode");

                Logger.LogMessage("Unlocked in the remote storage succesefully", UserFileSystemPath, default, operationContext);
            }
        }
    }
}
