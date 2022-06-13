using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;

namespace VirtualFileSystem
{
    ///<inheritdoc>
    public abstract class VirtualFileSystemItem : IFileSystemItem
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
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">File or folder path in the user file system.</param>
        /// <param name="remoteStorageItemId">Remote storage item ID.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFileSystemItem(string userFileSystemPath, byte[] remoteStorageItemId, ILogger logger)
        {
            UserFileSystemPath = string.IsNullOrEmpty(userFileSystemPath) ? throw new ArgumentNullException(nameof(userFileSystemPath)) : userFileSystemPath;
            RemoteStorageItemId = remoteStorageItemId ?? throw new ArgumentNullException(nameof(remoteStorageItemId));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        
        ///<inheritdoc/>
        public async Task MoveToAsync(string targetUserFileSystemPath, byte[] targetFolderRemoteStorageItemId, IOperationContext operationContext = null, IConfirmationResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewPath = targetUserFileSystemPath;
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogDebug($"{nameof(IFileSystemItem)}.{nameof(MoveToAsync)}()", userFileSystemOldPath, userFileSystemNewPath, operationContext);
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
                    if(File.Exists(remoteStorageNewPath))
                    {
                        File.Delete(remoteStorageNewPath);
                    }
                    (remoteStorageOldItem as FileInfo).MoveTo(remoteStorageNewPath);
                }
                else
                {
                    (remoteStorageOldItem as DirectoryInfo).MoveTo(remoteStorageNewPath);
                }

                Logger.LogMessage("Moved item in remote storage succesefully", userFileSystemOldPath, targetUserFileSystemPath, operationContext);
            }
        }
        

        
        ///<inheritdoc/>
        public async Task DeleteAsync(IOperationContext operationContext = null, IConfirmationResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogDebug($"{nameof(IFileSystemItem)}.{nameof(DeleteAsync)}()", this.UserFileSystemPath, default, operationContext);

            // To cancel the operation and prevent the file from being deleted, 
            // call the resultContext.ReturnErrorResult() method or throw any exception inside this method:
            // resultContext.ReturnErrorResult(CloudFileStatus.STATUS_CLOUD_FILE_REQUEST_TIMEOUT);

            // IMPOTRTANT!
            // Make sure you have all Windows updates installed.
            // See Windows Cloud API delete prevention bug description here: 
            // https://stackoverflow.com/questions/68887190/delete-in-cloud-files-api-stopped-working-on-windows-21h1
            // https://docs.microsoft.com/en-us/answers/questions/75240/bug-report-cfapi-ackdelete-borken-on-win10-2004.html
        }

        /// <inheritdoc/>
        public async Task DeleteCompletionAsync(IOperationContext operationContext = null, IInSyncStatusResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            // On Windows, for rename with overwrite to function properly for folders, 
            // the deletion of the folder in the remote storage must be done in DeleteCompletionAsync()
            // Otherwise the source folder will be deleted before files in it can be moved.

            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteCompletionAsync)}()", this.UserFileSystemPath, default, operationContext);

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
                    Logger.LogDebug("Deleted item in remote storage succesefully", UserFileSystemPath, default, operationContext);
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
        

        ///<inheritdoc/>
        public Task<byte[]> GetThumbnailAsync(uint size)
        {
            // For this method to be called you need to register a thumbnail handler.
            // See method description for more details.

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<FileSystemItemPropertyData>> GetPropertiesAsync()
        {
            // For this method to be called you need to register a properties handler.
            // See method description for more details.

            throw new NotImplementedException();
        }

        ///<inheritdoc/>
        public Task<IFileSystemItemMetadata> GetMetadataAsync()
        {
            // Return IFileMetadata for a file, IFolderMetadata for a folder.
            throw new NotImplementedException();
        }
    }
}
