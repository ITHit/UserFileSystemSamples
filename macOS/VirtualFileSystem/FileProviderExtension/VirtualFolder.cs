using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;
using System.Threading;
using System.Text;

namespace FileProviderExtension
{

    /// <inheritdoc cref="IFolder"/>
    public class VirtualFolder : VirtualFileSystemItem, IFolder
    {

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="remoteStoragePath">File or folder path in the remote system.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFolder(string remoteStoragePath, ILogger logger) : base(remoteStoragePath, logger)
        {

        }

        /// <inheritdoc/>
        public async Task<IFileMetadata> CreateFileAsync(IFileMetadata fileMetadata, Stream? content = null, IOperationContext operationContext = null, IInSyncResultContext? inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}()", Path.Combine(RemoteStoragePath, fileMetadata.Name));

            FileInfo remoteStorageItem = new FileInfo(Path.Combine(RemoteStoragePath, fileMetadata.Name));

            // Upload remote storage file content.
            await using (FileStream remoteStorageStream = remoteStorageItem.Open(FileMode.CreateNew, FileAccess.Write, FileShare.Delete))
            {
                if (content != null)
                {
                    await content.CopyToAsync(remoteStorageStream);
                    remoteStorageStream.SetLength(content.Length);
                }
            }

            // Update remote storage file metadata.
            remoteStorageItem.Attributes = fileMetadata.Attributes ?? new();
            remoteStorageItem.CreationTimeUtc = fileMetadata.CreationTime?.UtcDateTime ?? DateTime.UtcNow;
            remoteStorageItem.LastWriteTimeUtc = fileMetadata.LastWriteTime?.UtcDateTime ?? DateTime.UtcNow;
            remoteStorageItem.LastAccessTimeUtc = fileMetadata.LastAccessTime?.UtcDateTime ?? DateTime.UtcNow;
            remoteStorageItem.LastWriteTimeUtc = fileMetadata.LastWriteTime?.UtcDateTime ?? DateTime.UtcNow;

            return new FileMetadataMac
            {
                RemoteStorageItemId = Mapping.EncodePath(remoteStorageItem.FullName)
            };
        }

        /// <inheritdoc/>
        public async Task<IFolderMetadata> CreateFolderAsync(IFolderMetadata folderMetadata, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}()", Path.Combine(RemoteStoragePath, folderMetadata.Name));

            DirectoryInfo remoteStorageItem = new DirectoryInfo(Path.Combine(RemoteStoragePath, folderMetadata.Name));
            remoteStorageItem.Create();

            // Update remote storage folder metadata.
            remoteStorageItem.Attributes = folderMetadata.Attributes ?? new();
            remoteStorageItem.CreationTimeUtc = folderMetadata.CreationTime?.UtcDateTime ?? DateTime.UtcNow;
            remoteStorageItem.LastWriteTimeUtc = folderMetadata.LastWriteTime?.UtcDateTime ?? DateTime.UtcNow;
            remoteStorageItem.LastAccessTimeUtc = folderMetadata.LastAccessTime?.UtcDateTime ?? DateTime.UtcNow;
            remoteStorageItem.LastWriteTimeUtc = folderMetadata.LastWriteTime?.UtcDateTime ?? DateTime.UtcNow;

            return new FolderMetadataMac
            {
                RemoteStorageItemId = Mapping.EncodePath(remoteStorageItem.FullName)
            };
        }

        /// <inheritdoc/>
        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext, CancellationToken cancellationToken)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChildrenAsync)}({pattern})", RemoteStoragePath);

            IEnumerable<FileSystemInfo> remoteStorageChildren = new DirectoryInfo(RemoteStoragePath).EnumerateFileSystemInfos(pattern);

            List<IMetadata> userFileSystemChildren = new List<IMetadata>();
            foreach (FileSystemInfo remoteStorageItem in remoteStorageChildren)
            {
                IMetadata itemInfo = Mapping.GetUserFileSysteItemMetadata(remoteStorageItem);
                userFileSystemChildren.Add(itemInfo);
            }

            // To signal that the children enumeration is completed 
            // always call ReturnChildren(), even if the folder is empty.
            await resultContext.ReturnChildrenAsync(userFileSystemChildren.ToArray(), userFileSystemChildren.Count);
        }


        /// <inheritdoc/>
        public async Task<IFolderMetadata> WriteAsync(IFolderMetadata folderMetadata, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(WriteAsync)}()", RemoteStoragePath);

            DirectoryInfo remoteStorageItem = new DirectoryInfo(RemoteStoragePath);

            // Update remote storage folder metadata.
            if (folderMetadata.Attributes != null)
            {
                remoteStorageItem.Attributes = folderMetadata.Attributes.Value;
            }

            if (folderMetadata.CreationTime != null)
            {
                remoteStorageItem.CreationTimeUtc = folderMetadata.CreationTime.Value.UtcDateTime;
            }

            if (folderMetadata.LastWriteTime != null)
            {
                remoteStorageItem.LastWriteTimeUtc = folderMetadata.LastWriteTime.Value.UtcDateTime;
            }

            if (folderMetadata.LastAccessTime != null)
            {
                remoteStorageItem.LastAccessTimeUtc = folderMetadata.LastAccessTime.Value.UtcDateTime;
            }          

            return await GetMetadataAsync(null,null) as IFolderMetadata;
        }
    }
    
}
