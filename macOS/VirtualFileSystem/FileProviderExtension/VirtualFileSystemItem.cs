using System.Text;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;

namespace FileProviderExtension
{
    public abstract class VirtualFileSystemItem : IFileSystemItemMac
    {
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
        /// <param name="remoteStoragePath">File or folder path in the remote system.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFileSystemItem(string remoteStoragePath, ILogger logger)
        {
            if (string.IsNullOrEmpty(remoteStoragePath))
            {
                throw new ArgumentNullException(nameof(remoteStoragePath));
            }
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            RemoteStoragePath = remoteStoragePath;
        }


        ///<inheritdoc>
        public async Task MoveToAsync(string targetUserFileSystemPath, byte[] targetFolderRemoteStorageItemId, IOperationContext operationContext = null, IConfirmationResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToAsync)}()", RemoteStoragePath, targetUserFileSystemPath);

            string targetStorageItemName = targetUserFileSystemPath.Split(Path.DirectorySeparatorChar).Last();
            string remoteStorageNewPath = Path.Combine(Mapping.DecodePath(targetFolderRemoteStorageItemId), targetStorageItemName);
            string remoteStorageOldPath = RemoteStoragePath;

            if (File.Exists(RemoteStoragePath))
            {
                new FileInfo(RemoteStoragePath).MoveTo(remoteStorageNewPath);
            }
            else if (Directory.Exists(RemoteStoragePath))
            {
                new DirectoryInfo(RemoteStoragePath).MoveTo(remoteStorageNewPath);
            }

            (operationContext as IMacMoveOperationContext).SetRemoteStorageItemId(Mapping.EncodePath(remoteStorageNewPath));

            Logger.LogMessage("Moved item in remote storage succesefully", RemoteStoragePath, targetUserFileSystemPath);

        }


        ///<inheritdoc>
        public async Task DeleteAsync(IOperationContext operationContext = null, IConfirmationResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteAsync)}()", RemoteStoragePath);

            if (File.Exists(RemoteStoragePath))
            {
                File.Delete(RemoteStoragePath);
            }
            else if (Directory.Exists(RemoteStoragePath))
            {
                Directory.Delete(RemoteStoragePath, true);
            }
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

        ///<inheritdoc>
        public async Task<byte[]> GetThumbnailAsync(uint size, IOperationContext operationContext)
        {
            byte[] content = null;

            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(GetThumbnailAsync)}({size})", RemoteStoragePath);

            string[] validExtensions = { ".jpg", ".bmp", ".gif", ".png", ".jpeg" };
            if (validExtensions.Contains(Path.GetExtension(RemoteStoragePath)))
            {
                content = File.ReadAllBytes(RemoteStoragePath);
            }

            return content;
        }

        ///<inheritdoc>
        public Task<IEnumerable<FileSystemItemPropertyData>> GetPropertiesAsync(IOperationContext operationContext)
        {
            throw new NotImplementedException();
        }
    }
}
