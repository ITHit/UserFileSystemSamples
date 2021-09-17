using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
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

            FileSystemInfo remoteStorageOldItem = FsPath.GetFileSystemItem(remoteStorageOldPath);
            if (remoteStorageOldItem != null)
            {
                if (remoteStorageOldItem is FileInfo)
                {
                    (remoteStorageOldItem as FileInfo).MoveTo(remoteStorageNewPath, true);
                }
                else
                {
                    (remoteStorageOldItem as DirectoryInfo).MoveTo(remoteStorageNewPath);
                }
                Logger.LogMessage("Moved item in remote storage succesefully", userFileSystemOldPath, userFileSystemNewPath, operationContext);
            }

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

            FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(RemoteStoragePath);
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
                Logger.LogMessage("Deleted item in remote storage succesefully", UserFileSystemPath, default, operationContext);
            }

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
                // Supply the lock-token as part of each remote storage update in File.WriteAsync() method.
                // For demo purposes we just fill some generic data.
                ServerLockInfo lockInfo = new ServerLockInfo() { LockToken = "ServerToken", Owner = "You", Exclusive = true, LockExpirationDateUtc = DateTimeOffset.Now.AddMinutes(30) };

                // Save lock-token and lock-mode.
                await lockManager.SetLockInfoAsync(lockInfo);
                await lockManager.SetLockModeAsync(lockMode);

                // Set lock icon and lock info in custom columns.
                await customDataManager.SetLockInfoAsync(lockInfo);

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

            // Unlock the item in the remote storage here.

            // Delete lock-mode and lock-token info.
            lockManager.DeleteLock();

            // Remove lock icon and lock info in custom columns.
            await customDataManager.SetLockInfoAsync(null);

            Logger.LogMessage("Unlocked in the remote storage succesefully", UserFileSystemPath);
        }

    }
}
