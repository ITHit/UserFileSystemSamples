using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;


namespace VirtualDrive
{
    /// <inheritdoc cref="IFolderWindows"/>
    public class VirtualFolder : VirtualFileSystemItem, IFolder
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="path">Folder path in the user file system.</param>
        /// <param name="remoteStorageItemId">Remote storage item ID.</param>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFolder(string path, byte[] remoteStorageItemId, VirtualEngine engine, ILogger logger) : base(path, remoteStorageItemId, engine, logger)
        {

        }

        /// <inheritdoc/>
        public async Task<IFileMetadata> CreateFileAsync(IFileMetadata fileMetadata, Stream content = null, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, fileMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}()", userFileSystemNewItemPath);

            if (!Mapping.TryGetRemoteStoragePathById(RemoteStorageItemId, out string remoteStoragePath))
            {
                // Possibly parent is deleted in the remote storage.
                Logger.LogMessage($"Can't create. Parent not found", userFileSystemNewItemPath, BitConverter.ToString(RemoteStorageItemId).Replace("-", ""));
                inSyncResultContext.SetInSync = false;
                return null;
            }

            FileInfo remoteStorageNewItem = new FileInfo(Path.Combine(remoteStoragePath, fileMetadata.Name));

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


            // Return newly created item to the Engine.
            // In the returned data set the following fields:
            //  - Remote storage item ID. It will be passed to GetFileSystemItem() during next calls.
            //  - Content eTag. The Engine will store it to determine if the file content should be updated.
            //  - Medatdata eTag. The Engine will store it to determine if the item metadata should be updated.
            byte[] remoteStorageId = WindowsFileSystemItem.GetItemIdByPath(remoteStorageNewItem.FullName);
            return new FileMetadataExt()
            {
                RemoteStorageItemId = remoteStorageId,
                // ContentETag = 
                // MetadataETag =
            };
        }

        /// <inheritdoc/>
        public async Task<IFolderMetadata> CreateFolderAsync(IFolderMetadata folderMetadata, IOperationContext operationContext, IInSyncResultContext inSyncResultContext, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, folderMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}()", userFileSystemNewItemPath);

            if (!Mapping.TryGetRemoteStoragePathById(RemoteStorageItemId, out string remoteStoragePath))
            {
                // Possibly parent is deleted in the remote storage.
                Logger.LogMessage($"Can't create. Parent not found", userFileSystemNewItemPath, BitConverter.ToString(RemoteStorageItemId).Replace("-", ""));
                inSyncResultContext.SetInSync = false;
                return null;
            }

            DirectoryInfo remoteStorageNewItem = new DirectoryInfo(Path.Combine(remoteStoragePath, folderMetadata.Name));
            remoteStorageNewItem.Create();

            // Update remote storage folder metadata.
            remoteStorageNewItem.Attributes = folderMetadata.Attributes & ~FileAttributes.ReadOnly;
            remoteStorageNewItem.CreationTimeUtc = folderMetadata.CreationTime.UtcDateTime;
            remoteStorageNewItem.LastWriteTimeUtc = folderMetadata.LastWriteTime.UtcDateTime;
            remoteStorageNewItem.LastAccessTimeUtc = folderMetadata.LastAccessTime.UtcDateTime;
            remoteStorageNewItem.Attributes = folderMetadata.Attributes;

            byte[] remoteStorageId = WindowsFileSystemItem.GetItemIdByPath(remoteStorageNewItem.FullName);
            return new FolderMetadataExt()
            {
                RemoteStorageItemId = remoteStorageId
                // MetadataETag =
            };
        }

        /// <inheritdoc/>
        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext, CancellationToken cancellationToken)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call one of the following:
            // - resultContext.ReturnChildren() method.
            // - resultContext.ReportProgress() method.

            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChildrenAsync)}({pattern})", UserFileSystemPath, default, operationContext);

            cancellationToken.Register(() => { Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChildrenAsync)}({pattern}) cancelled", UserFileSystemPath, default, operationContext); });

            List<IFileSystemItemMetadata> children = new List<IFileSystemItemMetadata>();

            if (Mapping.TryGetRemoteStoragePathById(RemoteStorageItemId, out string remoteStoragePath))
            {
                IEnumerable<FileSystemInfo> remoteStorageChildren = new DirectoryInfo(remoteStoragePath).EnumerateFileSystemInfos(pattern);
                foreach (FileSystemInfo remoteStorageItem in remoteStorageChildren)
                {
                    IFileSystemItemMetadata itemInfo = Mapping.GetUserFileSysteItemMetadata(remoteStorageItem);
                    children.Add(itemInfo);
                }
            }

            // To signal that the children enumeration is completed 
            // always call ReturnChildren(), even if the folder is empty.
            await resultContext.ReturnChildrenAsync(children.ToArray(), children.Count(), true, cancellationToken);
        }

        public async Task<IEnumerable<FileSystemItemMetadataExt>> EnumerateChildrenAsync(string pattern, IOperationContext operationContext, CancellationToken cancellationToken)
        {
            if (!Mapping.TryGetRemoteStoragePathById(RemoteStorageItemId, out string remoteStoragePath)) return Enumerable.Empty<FileSystemItemMetadataExt>();
            var userFileSystemChildren = new System.Collections.Concurrent.ConcurrentBag<FileSystemItemMetadataExt>();
            IEnumerable<FileSystemInfo> remoteStorageChildren = new DirectoryInfo(remoteStoragePath).EnumerateFileSystemInfos(pattern);

            //Parallel.ForEach(remoteStorageChildren, new ParallelOptions() { CancellationToken = cancellationToken }, async (remoteStorageItem) =>
            foreach (FileSystemInfo remoteStorageItem in remoteStorageChildren)
            {
                FileSystemItemMetadataExt itemInfo = Mapping.GetUserFileSysteItemMetadata(remoteStorageItem);
                userFileSystemChildren.Add(itemInfo);
            }

            return userFileSystemChildren;
        }

        /// <inheritdoc/>
        public async Task<IFolderMetadata> WriteAsync(IFileSystemBasicInfo fileBasicInfo, IOperationContext operationContext, IInSyncResultContext inSyncResultContext, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(WriteAsync)}()", UserFileSystemPath, default, operationContext);

            if (!Mapping.TryGetRemoteStoragePathById(RemoteStorageItemId, out string remoteStoragePath)) return null;
            DirectoryInfo remoteStorageItem = new DirectoryInfo(remoteStoragePath);

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
