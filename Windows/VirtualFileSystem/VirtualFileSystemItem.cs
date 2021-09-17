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
        protected readonly byte[] ItemId;

        /// <summary>
        /// Path of this file or folder in the remote storage.
        /// </summary>
        protected readonly string RemoteStoragePath;

        /// <summary>
        /// Logger.
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">File or folder path in the user file system.</param>
        /// <param name="itemId">Remote storage item ID.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFileSystemItem(string userFileSystemPath, byte[] itemId, ILogger logger)
        {
            if (string.IsNullOrEmpty(userFileSystemPath))
            {
                throw new ArgumentNullException(nameof(userFileSystemPath));
            }
            ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            UserFileSystemPath = userFileSystemPath;

            try
            {
                RemoteStoragePath = WindowsFileSystemItem.GetPathByItemId(ItemId);
            }
            catch(ArgumentException)
            {
                // When a file is deleted, the IFile.CloseAsync() is called for the deleted file.
            }
        }

        
        ///<inheritdoc>
        public async Task MoveToAsync(string userFileSystemNewPath, byte[] newParentItemId, IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToAsync)}()", userFileSystemOldPath, userFileSystemNewPath, operationContext);

            string remoteStorageOldPath = RemoteStoragePath;
            FileSystemInfo remoteStorageOldItem = FsPath.GetFileSystemItem(remoteStorageOldPath);

            // newParentItemId is null if the hydrated file is moved outside of the virtual file system, for example to the recycle bin.
            if ( (remoteStorageOldItem != null) && (newParentItemId != null) )
            {
                string remoteStorageNewParentPath = WindowsFileSystemItem.GetPathByItemId(newParentItemId);
                string remoteStorageNewPath = Path.Combine(remoteStorageNewParentPath, Path.GetFileName(userFileSystemNewPath));

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
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteAsync)}()", this.UserFileSystemPath, default, operationContext);

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
            // On Windows, for rename with overwrite to function properly for folders, 
            // the deletion of the folder in the remote storage must be done in DeleteCompletionAsync()
            // Otherwise the folder will be deleted before files in it can be moved.

            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteCompletionAsync)}()", this.UserFileSystemPath, default, operationContext);

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
    }
}
