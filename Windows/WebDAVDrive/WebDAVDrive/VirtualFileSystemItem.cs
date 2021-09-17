using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;
using ITHit.WebDAV.Client;

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
        public async Task MoveToAsync(string userFileSystemNewPath, byte[] targetParentItemId, IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToAsync)}()", userFileSystemOldPath, userFileSystemNewPath, operationContext);

            string remoteStorageOldPath = RemoteStoragePath;
            string remoteStorageNewPath = Mapping.MapPath(userFileSystemNewPath);

            await Program.DavClient.MoveToAsync(new Uri(remoteStorageOldPath), new Uri(remoteStorageNewPath), true);

            await Engine.CustomDataManager(userFileSystemOldPath, Logger).MoveToAsync(userFileSystemNewPath);
        }

        /// <inheritdoc/>
        public async Task MoveToCompletionAsync(IMoveCompletionContext moveCompletionContext, IResultContext resultContext)
        {
            string userFileSystemNewPath = this.UserFileSystemPath;
            string userFileSystemOldPath = moveCompletionContext.SourcePath;
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToCompletionAsync)}()", userFileSystemOldPath, userFileSystemNewPath, moveCompletionContext);
        }

        ///<inheritdoc>
        public async Task DeleteAsync(IOperationContext operationContext, IConfirmationResultContext resultContext)
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
        public async Task DeleteCompletionAsync(IOperationContext operationContext, IResultContext resultContext)
        {
            // On Windows, for move with overwrite on folders to function correctly, 
            // the deletion of the folder in the remote storage must be done in DeleteCompletionAsync()
            // Otherwise the folder will be deleted before files in it can be moved.

            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteCompletionAsync)}()", UserFileSystemPath, default, operationContext);

            await Program.DavClient.DeleteAsync(new Uri(RemoteStoragePath));

            Engine.CustomDataManager(UserFileSystemPath, Logger).Delete();
        }

        ///<inheritdoc>
        public Task<IFileSystemItemMetadata> GetMetadataAsync()
        {
            // Return IFileMetadata for a file, IFolderMetadata for a folder.
            throw new NotImplementedException();
        }

        /// <summary>
        /// Simulates network delays and reports file transfer progress for demo purposes.
        /// </summary>
        /// <param name="fileLength">Length of file.</param>
        /// <param name="context">Context to report progress to.</param>
        protected void SimulateNetworkDelay(long fileLength, IResultContext resultContext)
        {
            if (Program.Settings.NetworkSimulationDelayMs > 0)
            {
                int numProgressResults = 5;
                for (int i = 0; i < numProgressResults; i++)
                {
                    resultContext.ReportProgress(fileLength, i * fileLength / numProgressResults);
                    Thread.Sleep(Program.Settings.NetworkSimulationDelayMs);
                }
            }
        }

        
        ///<inheritdoc>
        public async Task LockAsync(LockMode lockMode)
        {
            Logger.LogMessage($"{nameof(ILock)}.{nameof(LockAsync)}()", UserFileSystemPath);

            ExternalDataManager customDataManager = Engine.CustomDataManager(UserFileSystemPath, Logger);
            LockManager lockManager = customDataManager.LockManager;
            if (!await lockManager.IsLockedAsync()
                && !Engine.CustomDataManager(UserFileSystemPath).IsNew)
            {
                // Set pending icon, so the user has a feedback as lock operation may take some time.
                await customDataManager.SetLockPendingIconAsync(true);

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
                await lockManager.SetLockInfoAsync(serverLockInfo);
                await lockManager.SetLockModeAsync(lockMode);

                // Set lock icon and lock info in custom columns.
                await customDataManager.SetLockInfoAsync(serverLockInfo);

                Logger.LogMessage("Locked in remote storage succesefully.", UserFileSystemPath);
            }
        }
        

        
        ///<inheritdoc>
        public async Task<LockMode> GetLockModeAsync()
        {
            LockManager lockManager = Engine.CustomDataManager(UserFileSystemPath, Logger).LockManager;
            return await lockManager.GetLockModeAsync();
        }
        

        
        ///<inheritdoc>
        public async Task UnlockAsync()
        {
            if (MsOfficeHelper.IsMsOfficeLocked(UserFileSystemPath)) // Required for PowerPoint. It does not block the for writing.
            {
                throw new ClientLockFailedException("The file is blocked for writing.");
            }

            ExternalDataManager customDataManager = Engine.CustomDataManager(UserFileSystemPath, Logger);
            LockManager lockManager = customDataManager.LockManager;

            // Set pending icon, so the user has a feedback as unlock operation may take some time.
            await customDataManager.SetLockPendingIconAsync(true);

            // Read lock-token from lock-info file.
            string lockToken = (await lockManager.GetLockInfoAsync()).LockToken;
            LockUriTokenPair[] lockTokens = new LockUriTokenPair[] { new LockUriTokenPair(new Uri(RemoteStoragePath), lockToken)};

            // Unlock the item in the remote storage.
            try
            {
                await Program.DavClient.UnlockAsync(new Uri(RemoteStoragePath), lockTokens);
            }
            catch (ITHit.WebDAV.Client.Exceptions.ConflictException)
            {
                // The item is already unlocked.
            }

            // Delete lock-mode and lock-token info.
            lockManager.DeleteLock();

            // Remove lock icon and lock info in custom columns.
            await customDataManager.SetLockInfoAsync(null);

            Logger.LogMessage("Unlocked in the remote storage succesefully", UserFileSystemPath);
        }
        
    }
}
