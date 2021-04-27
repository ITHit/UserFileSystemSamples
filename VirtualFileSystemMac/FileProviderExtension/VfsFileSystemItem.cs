using ITHit.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileProviderExtension
{
    // In most cases you can use this class in your project without any changes.
    ///<inheritdoc>
    public abstract class VfsFileSystemItem : IFileSystemItem, IFileSystemItemMetadata
    {
        /// <summary>
        /// File or folder path in the user file system.
        /// </summary>
        protected readonly string UserFileSystemPath;

        /// <summary>
        /// Path of this file or folder in the remote storage.
        /// </summary>
        protected string RemoteStoragePath;

        /// <summary>
        /// Logger.
        /// </summary>
        protected readonly ILogger Logger;

        ///<inheritdoc/>
        public string Name { get; set; }

        ///<inheritdoc/>
        public FileAttributes Attributes { get; set; }

        ///<inheritdoc/>
        public byte[] CustomData { get; set; }

        ///<inheritdoc/>
        public DateTimeOffset CreationTime { get; set; }

        ///<inheritdoc/>
        public DateTimeOffset LastWriteTime { get; set; }

        ///<inheritdoc/>
        public DateTimeOffset LastAccessTime { get; set; }

        ///<inheritdoc/>
        public DateTimeOffset ChangeTime { get; set; }

        /// <summary>
        /// Server ETag.
        /// </summary>
        public string ETag { get; set; }

        /// <summary>
        /// Indicates if the item is locked by another user in the remote storage.
        /// </summary>
        public bool LockedByAnotherUser { get; set; }

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">File or folder path in user file system.</param>
        /// <param name="logger">Logger.</param>
        public VfsFileSystemItem(string userFileSystemPath, ILogger logger)
        {
            if (string.IsNullOrEmpty(userFileSystemPath))
            {
                throw new ArgumentNullException("userFileSystemPath");
            }

            UserFileSystemPath = userFileSystemPath;
            RemoteStoragePath = Mapping.MapPath(userFileSystemPath);
            Logger = logger;
        }

        
        ///<inheritdoc>
        public async Task MoveToAsync(string userFileSystemNewPath, IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            Logger.LogMessage($"IFileSystemItem.MoveToAsync", UserFileSystemPath, userFileSystemNewPath);
            string remoteStorageNewPath = Mapping.MapPath(userFileSystemNewPath);

            if (File.Exists(RemoteStoragePath))
            {
                new FileInfo(RemoteStoragePath).MoveTo(remoteStorageNewPath);
            }
            else if(Directory.Exists(RemoteStoragePath))
            {
                new DirectoryInfo(RemoteStoragePath).MoveTo(remoteStorageNewPath);
            }
        }
        

        
        ///<inheritdoc>
        public async Task DeleteAsync(IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            Logger.LogMessage($"IFileSystemItem.DeleteAsync", UserFileSystemPath);

            if (File.Exists(RemoteStoragePath))
            {
                File.Delete(RemoteStoragePath);
            }
            else if(Directory.Exists(RemoteStoragePath))
            {
                Directory.Delete(RemoteStoragePath, true);
            }
        }
        

        /// <summary>
        /// Simulates network delays and reports file transfer progress for demo purposes.
        /// </summary>
        /// <param name="fileLength">Length of file.</param>
        /// <param name="context">Context to report progress to.</param>
        protected void SimulateNetworkDelay(long fileLength, IResultContext resultContext)
        {
            throw new NotImplementedException();
        }

        public Task MoveToCompletionAsync(IMoveCompletionContext moveCompletionContext, IResultContext resultContext)
        {
            throw new NotImplementedException();
        }

        public Task DeleteCompletionAsync(IOperationContext operationContext, IResultContext resultContext)
        {
            throw new NotImplementedException();
        }

        public Task<IFileSystemItemMetadata> GetMetadataAsync()
        {
            throw new NotImplementedException();
        }
    }
}
