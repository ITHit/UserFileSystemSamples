using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Windows;
using ITHit.WebDAV.Client;
using ITHit.WebDAV.Client.Exceptions;

namespace WebDAVDrive
{
    ///<inheritdoc>
    public abstract class VirtualFileSystemItem : IFileSystemItem, ILock
    {
        /// <summary>
        /// File or folder path in the user file system.
        /// </summary>
        protected readonly string UserFileSystemPath;

        /// <summary>
        /// Path of this file or folder in the remote storage.
        /// </summary>
        protected readonly string RemoteStoragePath;

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
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFileSystemItem(string userFileSystemPath, VirtualEngine engine, ILogger logger)
        {
            if (string.IsNullOrEmpty(userFileSystemPath))
            {
                throw new ArgumentNullException(nameof(userFileSystemPath));
            }
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Engine = engine ?? throw new ArgumentNullException(nameof(engine));

            UserFileSystemPath = userFileSystemPath;
            RemoteStoragePath = Mapping.MapPath(userFileSystemPath);
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

            string remoteStorageOldPath = RemoteStoragePath;
            string remoteStorageNewPath = Mapping.MapPath(userFileSystemNewPath);

            await Program.DavClient.MoveToAsync(new Uri(remoteStorageOldPath), new Uri(remoteStorageNewPath), true);
            Logger.LogMessage("Moved in the remote storage succesefully", userFileSystemOldPath, targetUserFileSystemPath, operationContext);
        }

        ///<inheritdoc>
        public async Task DeleteAsync(IOperationContext operationContext, IConfirmationResultContext resultContext, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteAsync)}()", UserFileSystemPath, default, operationContext);

            // To cancel the operation and prevent the file from being deleted, 
            // call the resultContext.ReturnErrorResult() method or throw any exception inside this method.

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

            try
            {
                await Program.DavClient.DeleteAsync(new Uri(RemoteStoragePath));
                Logger.LogMessage("Deleted in the remote storage succesefully", UserFileSystemPath, default, operationContext);
            }
            catch (WebDavHttpException ex)
            {
                // Windows Explorer may call delete more than one time on the same file/folder.
                Logger.LogMessage(ex.Message);
            }
        }

        ///<inheritdoc>
        public Task<IFileSystemItemMetadata> GetMetadataAsync()
        {
            // Return IFileMetadata for a file, IFolderMetadata for a folder.
            throw new System.NotImplementedException();
        }       

        
        ///<inheritdoc>
        public async Task LockAsync(LockMode lockMode, IOperationContext operationContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(ILock)}.{nameof(LockAsync)}()", UserFileSystemPath, default, operationContext);

            // Call your remote storage here to lock the item.
            // Save the lock token and other lock info received from the remote storage on the client.
            // Supply the lock-token as part of each remote storage update in IFile.WriteAsync() method.

            LockInfo lockInfo = await Program.DavClient.LockAsync(new Uri(RemoteStoragePath), LockScope.Exclusive, false, null, TimeSpan.MaxValue);
            ServerLockInfo serverLockInfo = new ServerLockInfo
            {
                LockToken = lockInfo.LockToken.LockToken,
                Exclusive = lockInfo.LockScope == LockScope.Exclusive,
                Owner = lockInfo.Owner,
                LockExpirationDateUtc = DateTimeOffset.Now.Add(lockInfo.TimeOut)
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
            PlaceholderItem placeholder = Engine.Placeholders.GetItem(UserFileSystemPath);

            IDataItem property;
            if (placeholder.Properties.TryGetValue("LockMode", out property))
            {
                return await property.GetValueAsync<LockMode>();
            }
            else
            {
                return LockMode.None;
            }
        }
        

        
        ///<inheritdoc>
        public async Task UnlockAsync(IOperationContext operationContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(ILock)}.{nameof(UnlockAsync)}()", UserFileSystemPath, default, operationContext);

            // Read the lock-token.
            PlaceholderItem placeholder = Engine.Placeholders.GetItem(UserFileSystemPath);
            string lockToken = (await placeholder.Properties["LockInfo"].GetValueAsync<ServerLockInfo>())?.LockToken;

            LockUriTokenPair[] lockTokens = new LockUriTokenPair[] { new LockUriTokenPair(new Uri(RemoteStoragePath), lockToken)};

            // Unlock the item in the remote storage.
            try
            {
                await Program.DavClient.UnlockAsync(new Uri(RemoteStoragePath), lockTokens);
                Logger.LogMessage("Unlocked in the remote storage succesefully", UserFileSystemPath, default, operationContext);
            }
            catch (ITHit.WebDAV.Client.Exceptions.ConflictException)
            {
                // The item is already unlocked.
            }

            // Delete lock-mode and lock-token info.
            placeholder.Properties.Remove("LockInfo");
            placeholder.Properties.Remove("LockMode");
        }
        
    }
}
