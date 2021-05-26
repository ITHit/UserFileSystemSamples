using ITHit.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileProviderExtension
{
    public abstract class VirtualFileSystemItem : IFileSystemItem
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
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">File or folder path in the user file system.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFileSystemItem(string userFileSystemPath, ILogger logger)
        {
            if (string.IsNullOrEmpty(userFileSystemPath))
            {
                throw new ArgumentNullException(nameof(userFileSystemPath));
            }
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            UserFileSystemPath = userFileSystemPath;
            RemoteStoragePath = Mapping.MapPath(userFileSystemPath);
        }

        
        ///<inheritdoc>
        public async Task MoveToAsync(string userFileSystemNewPath, IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToAsync)}()", UserFileSystemPath, userFileSystemNewPath);
            string remoteStorageNewPath = Mapping.MapPath(userFileSystemNewPath);
            string remoteStorageOldPath = RemoteStoragePath;

            if (File.Exists(RemoteStoragePath))
            {
                new FileInfo(RemoteStoragePath).MoveTo(remoteStorageNewPath);
            }
            else if (Directory.Exists(RemoteStoragePath))
            {
                new DirectoryInfo(RemoteStoragePath).MoveTo(remoteStorageNewPath);
            }
            Logger.LogMessage("Moved item in remote storage succesefully", userFileSystemOldPath, userFileSystemNewPath);
        }
        

        /// <inheritdoc/>
        public async Task MoveToCompletionAsync(IMoveCompletionContext moveCompletionContext, IResultContext resultContext)
        {
            throw new NotImplementedException();
        }

        
        ///<inheritdoc>
        public async Task DeleteAsync(IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteAsync)}()", UserFileSystemPath);

            if (File.Exists(RemoteStoragePath))
            {
                File.Delete(RemoteStoragePath);
            }
            else if (Directory.Exists(RemoteStoragePath))
            {
                Directory.Delete(RemoteStoragePath, true);
            }
        }

        /// <inheritdoc/>
        public async Task DeleteCompletionAsync(IOperationContext operationContext, IResultContext resultContext)
        {
            throw new NotImplementedException();
        }
        

        ///<inheritdoc>
        public async Task<IFileSystemItemMetadata> GetMetadataAsync()
        {
            // Return IFileMetadata for a file, IFolderMetadata for a folder.
            
            if (File.Exists(RemoteStoragePath))
            {
                return Mapping.GetUserFileSysteItemMetadata(new FileInfo(RemoteStoragePath));
            }
            else if (Directory.Exists(RemoteStoragePath))
            {
                return Mapping.GetUserFileSysteItemMetadata(new DirectoryInfo(RemoteStoragePath));
            }

            return null;
        }

        /// <summary>
        /// Simulates network delays and reports file transfer progress for demo purposes.
        /// </summary>
        /// <param name="fileLength">Length of file.</param>
        /// <param name="context">Context to report progress to.</param>
        protected void SimulateNetworkDelay(long fileLength, IResultContext resultContext)
        {
            //Thread.Sleep(10000);
        }
    }
}
