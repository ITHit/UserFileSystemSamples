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
    public abstract class VirtualFileSystemItem : IFileSystemItemWindows
    {
        /// <summary>
        /// File or folder path in the user file system.
        /// </summary>
        protected readonly string UserFileSystemPath;

        /// <summary>
        /// File or folder path in the remote storage.
        /// </summary>
        protected readonly string RemoteStoragePath;

        /// <summary>
        /// Logger.
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Maps remote storage path to the user file system path and vice versa. 
        /// </summary>
        private readonly IMapping mapping;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="mapping">Maps a the remote storage path and data to the user file system path and data.</param>
        /// <param name="userFileSystemPath">File or folder path in the user file system.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFileSystemItem(IMapping mapping, string userFileSystemPath, ILogger logger)
        {
            this.mapping = mapping;
            UserFileSystemPath = string.IsNullOrEmpty(userFileSystemPath) ? throw new ArgumentNullException(nameof(userFileSystemPath)) : userFileSystemPath;
            RemoteStoragePath = mapping.MapPath(userFileSystemPath);
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        
        ///<inheritdoc/>
        public async Task MoveToAsync(string targetUserFileSystemPath, byte[] targetFolderRemoteStorageItemId, IOperationContext operationContext, IConfirmationResultContext resultContext, CancellationToken cancellationToken = default)
        {
            IWindowsMoveContext moveContext = operationContext as IWindowsMoveContext;
            string userFileSystemNewPath = moveContext.TargetPath;
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogDebug($"{nameof(IFileSystemItem)}.{nameof(MoveToAsync)}()", userFileSystemOldPath, userFileSystemNewPath, moveContext);
        }

        /// <inheritdoc/>
        public async Task MoveToCompletionAsync(string targetUserFileSystemPath, byte[] targetFolderRemoteStorageItemId, IWindowsMoveContext moveContext, IInSyncStatusResultContext resultContext, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewPath = moveContext.TargetPath; 
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToCompletionAsync)}()", userFileSystemOldPath, userFileSystemNewPath, moveContext);

            FileSystemInfo remoteStorageOldItem = FsPath.GetFileSystemItem(RemoteStoragePath);

            if (remoteStorageOldItem != null)
            {
                string remoteStorageNewPath = mapping.MapPath(userFileSystemNewPath);
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

                Logger.LogDebug("Moved in the remote storage successfully", userFileSystemOldPath, userFileSystemNewPath, moveContext);
            }
        }
        

        
        ///<inheritdoc/>
        public async Task DeleteAsync(IOperationContext operationContext, IConfirmationResultContext resultContext = null, CancellationToken cancellationToken = default)
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
        public async Task DeleteCompletionAsync(IOperationContext operationContext, IInSyncStatusResultContext resultContext, CancellationToken cancellationToken = default)
        {
            // On Windows, for rename with overwrite to function properly for folders, 
            // the deletion of the folder in the remote storage must be done in DeleteCompletionAsync()
            // Otherwise the source folder will be deleted before files in it can be moved.

            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteCompletionAsync)}()", UserFileSystemPath, default, operationContext);

            try
            {
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
                    Logger.LogDebug("Deleted in the remote storage successfully", UserFileSystemPath, default, operationContext);
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
        public Task<byte[]> GetThumbnailAsync(uint size, IOperationContext operationContext)
        {
            // For this method to be called you need to register a thumbnail handler.
            // See method description for more details.

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<FileSystemItemPropertyData>> GetPropertiesAsync(IOperationContext operationContext)
        {
            // For this method to be called you need to register a properties handler.
            // See method description for more details.

            throw new NotImplementedException();
        }
    }
}
