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
        public async Task MoveToAsync(string targetUserFileSystemPath, byte[] targetParentItemId, IOperationContext operationContext = null, IConfirmationResultContext resultContext = null)
        {
            string userFileSystemNewPath = targetUserFileSystemPath;
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToAsync)}()", userFileSystemOldPath, userFileSystemNewPath, operationContext);
        }

        /// <inheritdoc/>
        public async Task MoveToCompletionAsync(string targetUserFileSystemPath, byte[] targetFolderRemoteStorageItemId, IMoveCompletionContext operationContext = null, IInSyncStatusResultContext resultContext = null)
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
                    (remoteStorageOldItem as FileInfo).MoveTo(remoteStorageNewPath, true);
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
        public async Task DeleteCompletionAsync(IOperationContext operationContext, IInSyncStatusResultContext resultContext)
        {
            // On Windows, for move with overwrite on folders to function correctly, 
            // the deletion of the folder in the remote storage must be done in DeleteCompletionAsync()
            // Otherwise the folder will be deleted before files in it can be moved.

            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteCompletionAsync)}()", UserFileSystemPath, default, operationContext);

            string remoteStoragePath;
            try
            {
                remoteStoragePath = Mapping.GetRemoteStoragePathById(RemoteStorageItemId);
            }
            catch (FileNotFoundException)
            {
                // Windows Explorer may call delete more than one time on the same file/folder.
                return;
            }

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
        public async Task LockAsync(LockMode lockMode, IOperationContext operationContext = null)
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
        public async Task<LockMode> GetLockModeAsync(IOperationContext operationContext = null)
        {
            PlaceholderItem placeholder = Engine.Placeholders.GetItem(UserFileSystemPath);

            IDataItem property;
            if(placeholder.Properties.TryGetValue("LockMode", out property))
            {
                return await property.GetValueAsync<LockMode>();
            }
            else
            {
                return LockMode.None;
            }
        }

        ///<inheritdoc>
        public async Task UnlockAsync(IOperationContext operationContext = null)
        {
            Logger.LogMessage($"{nameof(ILock)}.{nameof(UnlockAsync)}()", UserFileSystemPath, default, operationContext);

            // Read the lock-token.
            PlaceholderItem placeholder = Engine.Placeholders.GetItem(UserFileSystemPath);
            string lockToken = (await placeholder.Properties["LockInfo"].GetValueAsync<ServerLockInfo>())?.LockToken;

            // Unlock the item in the remote storage here.

            // Delete lock-mode and lock-token info.
            placeholder.Properties.Remove("LockInfo");
            placeholder.Properties.Remove("LockMode");

            Logger.LogMessage("Unlocked in the remote storage succesefully", UserFileSystemPath, default, operationContext);
        }
    }
}
