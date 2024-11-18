using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;


namespace VirtualFileSystem
{
    
    /// <inheritdoc cref="IFolder"/>
    public class VirtualFolder : VirtualFileSystemItem, IFolder
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="mapping">Maps a the remote storage path and data to the user file system path and data.</param>
        /// <param name="path">Folder path in the user file system.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFolder(IMapping mapping, string path, ILogger logger) : base(mapping, path, logger)
        {

        }

        /// <inheritdoc/>
        public async Task<IFileMetadata> CreateFileAsync(IFileMetadata metadata, Stream content = null, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, metadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}()", userFileSystemNewItemPath, default, operationContext, metadata);

            FileInfo remoteStorageNewItem = new FileInfo(Path.Combine(RemoteStoragePath, metadata.Name));

            // Create remote storage file.
            using (FileStream remoteStorageStream = remoteStorageNewItem.Open(FileMode.CreateNew, FileAccess.Write, FileShare.Delete))
            {
                // Upload content. Note that if the file is blocked - content parameter is null.
                if (content != null)
                {
                    try
                    {
                        await content.CopyToAsync(remoteStorageStream);
                    }
                    catch (OperationCanceledException)
                    {
                        // Operation was canceled by the calling Engine.StopAsync() or the operation timeout occurred.
                        Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}() canceled", userFileSystemNewItemPath, default, operationContext, metadata);
                    }
                    remoteStorageStream.SetLength(content.Length);
                }
            }

            // Update remote storage file metadata.
            remoteStorageNewItem.Attributes = metadata.Attributes.Value & ~FileAttributes.ReadOnly;
            remoteStorageNewItem.CreationTimeUtc = metadata.CreationTime.Value.UtcDateTime;
            remoteStorageNewItem.LastWriteTimeUtc = metadata.LastWriteTime.Value.UtcDateTime;
            remoteStorageNewItem.LastAccessTimeUtc = metadata.LastAccessTime.Value.UtcDateTime;
            remoteStorageNewItem.Attributes = metadata.Attributes.Value;

            // Typically you must return IFileMetadata with a remote storage item ID, content eTag and metadata eTag.
            // The ID will be passed later into IEngine.GetFileSystemItemAsync() method.
            // However, becuse we can not read the ID for the network path we return null.
            return null; 
        }

        /// <inheritdoc/>
        public async Task<IFolderMetadata> CreateFolderAsync(IFolderMetadata metadata, IOperationContext operationContext, IInSyncResultContext inSyncResultContext, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, metadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}()", userFileSystemNewItemPath, default, operationContext, metadata);

            DirectoryInfo remoteStorageNewItem = new DirectoryInfo(Path.Combine(RemoteStoragePath, metadata.Name));
            remoteStorageNewItem.Create();

            // Update remote storage folder metadata.
            remoteStorageNewItem.Attributes = metadata.Attributes.Value & ~FileAttributes.ReadOnly;
            remoteStorageNewItem.CreationTimeUtc = metadata.CreationTime.Value.UtcDateTime;
            remoteStorageNewItem.LastWriteTimeUtc = metadata.LastWriteTime.Value.UtcDateTime;
            remoteStorageNewItem.LastAccessTimeUtc = metadata.LastAccessTime.Value.UtcDateTime;
            remoteStorageNewItem.Attributes = metadata.Attributes.Value;

            // Typically you must return IFileMetadata with a remote storage item ID and metadata eTag.
            // The ID will be passed later into IEngine.GetFileSystemItemAsync() method.
            // However, becuse we can not read the ID for the network path we return null.
            return null;
        }

        /// <inheritdoc/>
        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext, CancellationToken cancellationToken)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timeout timer call one of the following:
            // - resultContext.ReturnChildren() method.
            // - resultContext.ReportProgress() method.

            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChildrenAsync)}({pattern})", UserFileSystemPath, default, operationContext);

            List<IFileSystemItemMetadata> children = new List<IFileSystemItemMetadata>();
            IEnumerable<FileSystemInfo> remoteStorageChildren = new DirectoryInfo(RemoteStoragePath).EnumerateFileSystemInfos(pattern);

            foreach (FileSystemInfo remoteStorageItem in remoteStorageChildren)
            {
                IFileSystemItemMetadata itemInfo = Mapping.GetUserFileSysteItemMetadata(remoteStorageItem);
                children.Add(itemInfo);
            }

            // To signal that the children enumeration is completed 
            // always call ReturnChildren(), even if the folder is empty.
            await resultContext.ReturnChildrenAsync(children.ToArray(), children.Count(), true, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IFolderMetadata> WriteAsync(IFolderMetadata metadata, IOperationContext operationContext, IInSyncResultContext inSyncResultContext, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(WriteAsync)}()", UserFileSystemPath, default, operationContext, metadata);

            DirectoryInfo remoteStorageItem = new DirectoryInfo(RemoteStoragePath);

            // Update remote storage folder metadata.
            if (metadata.Attributes.HasValue)
            {
                remoteStorageItem.Attributes = metadata.Attributes.Value & ~FileAttributes.ReadOnly;
            }

            if (metadata.CreationTime.HasValue)
            {
                remoteStorageItem.CreationTimeUtc = metadata.CreationTime.Value.UtcDateTime;
            }

            if (metadata.LastWriteTime.HasValue)
            {
                remoteStorageItem.LastWriteTimeUtc = metadata.LastWriteTime.Value.UtcDateTime;
            }

            if (metadata.LastAccessTime.HasValue)
            {
                remoteStorageItem.LastAccessTimeUtc = metadata.LastAccessTime.Value.UtcDateTime;
            }

            if (metadata.Attributes.HasValue)
            {
                remoteStorageItem.Attributes = metadata.Attributes.Value;
            }

            return null;
        }
    }
    
}
