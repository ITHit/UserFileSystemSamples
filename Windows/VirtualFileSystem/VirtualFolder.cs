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
        public async Task<IFileMetadata> CreateFileAsync(IFileMetadata fileMetadata, Stream content = null, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, fileMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}()", userFileSystemNewItemPath);

            FileInfo remoteStorageNewItem = new FileInfo(Path.Combine(RemoteStoragePath, fileMetadata.Name));

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
                        Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}() canceled", userFileSystemNewItemPath, default);
                    }
                    remoteStorageStream.SetLength(content.Length);
                }
            }

            // Update remote storage file metadata.
            remoteStorageNewItem.Attributes = fileMetadata.Attributes & ~FileAttributes.ReadOnly;
            remoteStorageNewItem.CreationTimeUtc = fileMetadata.CreationTime.UtcDateTime;
            remoteStorageNewItem.LastWriteTimeUtc = fileMetadata.LastWriteTime.UtcDateTime;
            remoteStorageNewItem.LastAccessTimeUtc = fileMetadata.LastAccessTime.UtcDateTime;
            remoteStorageNewItem.Attributes = fileMetadata.Attributes;

            // Typically you must return IFileMetadata with a remote storage item ID, content eTag and metadata eTag.
            // The ID will be passed later into IEngine.GetFileSystemItemAsync() method.
            // However, becuse we can not read the ID for the network path we return null.
            return null; 
        }

        /// <inheritdoc/>
        public async Task<IFolderMetadata> CreateFolderAsync(IFolderMetadata folderMetadata, IOperationContext operationContext, IInSyncResultContext inSyncResultContext, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, folderMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}()", userFileSystemNewItemPath);

            DirectoryInfo remoteStorageNewItem = new DirectoryInfo(Path.Combine(RemoteStoragePath, folderMetadata.Name));
            remoteStorageNewItem.Create();

            // Update remote storage folder metadata.
            remoteStorageNewItem.Attributes = folderMetadata.Attributes & ~FileAttributes.ReadOnly;
            remoteStorageNewItem.CreationTimeUtc = folderMetadata.CreationTime.UtcDateTime;
            remoteStorageNewItem.LastWriteTimeUtc = folderMetadata.LastWriteTime.UtcDateTime;
            remoteStorageNewItem.LastAccessTimeUtc = folderMetadata.LastAccessTime.UtcDateTime;
            remoteStorageNewItem.Attributes = folderMetadata.Attributes;

            // Typically you must return IFileMetadata with a remote storage item ID and metadata eTag.
            // The ID will be passed later into IEngine.GetFileSystemItemAsync() method.
            // However, becuse we can not read the ID for the network path we return null.
            return null;
        }

        /// <inheritdoc/>
        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext, CancellationToken cancellationToken)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call one of the following:
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
        public async Task<IFolderMetadata> WriteAsync(IFileSystemBasicInfo fileBasicInfo, IOperationContext operationContext, IInSyncResultContext inSyncResultContext, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(WriteAsync)}()", UserFileSystemPath, default, operationContext);

            DirectoryInfo remoteStorageItem = new DirectoryInfo(RemoteStoragePath);

            // Update remote storage folder metadata.
            if (fileBasicInfo.Attributes.HasValue)
            {
                remoteStorageItem.Attributes = fileBasicInfo.Attributes.Value & ~FileAttributes.ReadOnly;
            }

            if (fileBasicInfo.CreationTime.HasValue)
            {
                remoteStorageItem.CreationTimeUtc = fileBasicInfo.CreationTime.Value.UtcDateTime;
            }

            if (fileBasicInfo.LastWriteTime.HasValue)
            {
                remoteStorageItem.LastWriteTimeUtc = fileBasicInfo.LastWriteTime.Value.UtcDateTime;
            }

            if (fileBasicInfo.LastAccessTime.HasValue)
            {
                remoteStorageItem.LastAccessTimeUtc = fileBasicInfo.LastAccessTime.Value.UtcDateTime;
            }

            if (fileBasicInfo.Attributes.HasValue)
            {
                remoteStorageItem.Attributes = fileBasicInfo.Attributes.Value;
            }

            return null;
        }
    }
    
}
