using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Samples.Common;

namespace VirtualDrive
{
    /// <inheritdoc cref="IFolder"/>
    public class VirtualFolder : VirtualFileSystemItem, IFolder, IVirtualFolder
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="path">Folder path in the user file system.</param>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFolder(string path, VirtualEngine engine, ILogger logger) : base(path, engine, logger)
        {

        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFileAsync(IFileMetadata fileMetadata, Stream content = null)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, fileMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}()", userFileSystemNewItemPath);

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
            remoteStorageItem.Attributes = fileMetadata.Attributes;
            remoteStorageItem.CreationTimeUtc = fileMetadata.CreationTime.UtcDateTime;
            remoteStorageItem.LastWriteTimeUtc = fileMetadata.LastWriteTime.UtcDateTime;
            remoteStorageItem.LastAccessTimeUtc = fileMetadata.LastAccessTime.UtcDateTime;
            remoteStorageItem.LastWriteTimeUtc = fileMetadata.LastWriteTime.UtcDateTime;

            // Get ETag from server here and save it on the client.
            string eTagNew = "1234567890";

            ExternalDataManager customDataManager = Engine.ExternalDataManager(userFileSystemNewItemPath);
            // Store ETag unlil the next update.
            await customDataManager.SetCustomDataAsync(
                eTagNew,
                false,
                new[] { new FileSystemItemPropertyData((int)CustomColumnIds.ETag, eTagNew) });

            return null;
        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFolderAsync(IFolderMetadata folderMetadata)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, folderMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}()", userFileSystemNewItemPath);

            DirectoryInfo remoteStorageItem = new DirectoryInfo(Path.Combine(RemoteStoragePath, folderMetadata.Name));
            remoteStorageItem.Create();

            // Update remote storage folder metadata.
            remoteStorageItem.Attributes = folderMetadata.Attributes;
            remoteStorageItem.CreationTimeUtc = folderMetadata.CreationTime.UtcDateTime;
            remoteStorageItem.LastWriteTimeUtc = folderMetadata.LastWriteTime.UtcDateTime;
            remoteStorageItem.LastAccessTimeUtc = folderMetadata.LastAccessTime.UtcDateTime;
            remoteStorageItem.LastWriteTimeUtc = folderMetadata.LastWriteTime.UtcDateTime;

            // Get ETag from server here and save it on the client.
            string eTagNew = "1234567890";

            ExternalDataManager customDataManager = Engine.ExternalDataManager(userFileSystemNewItemPath);
            await customDataManager.SetCustomDataAsync(
                eTagNew,
                false,
                new[] { new FileSystemItemPropertyData((int)CustomColumnIds.ETag, eTagNew) });

            return null;
        }

        /// <inheritdoc/>
        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call one of the following:
            // - resultContext.ReturnChildren() method.
            // - resultContext.ReportProgress() method.

            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChildrenAsync)}({pattern})", UserFileSystemPath, default, operationContext);

            IEnumerable<FileSystemItemMetadataExt> remoteStorageChildren = await EnumerateChildrenAsync(pattern);

            List<FileSystemItemMetadataExt> userFileSystemChildren = new List<FileSystemItemMetadataExt>();
            foreach (FileSystemItemMetadataExt itemMetadata in remoteStorageChildren)
            {
                string userFileSystemItemPath = Path.Combine(UserFileSystemPath, itemMetadata.Name);

                // Filtering existing files/folders. This is only required to avoid extra errors in the log.
                if (!FsPath.Exists(userFileSystemItemPath))
                {
                    Logger.LogMessage("Creating", userFileSystemItemPath);
                    userFileSystemChildren.Add(itemMetadata);
                }

                ExternalDataManager customDataManager = Engine.ExternalDataManager(userFileSystemItemPath);

                // Mark this item as not new, which is required for correct MS Office saving opertions.
                customDataManager.IsNew = false;
            }

            // To signal that the children enumeration is completed 
            // always call ReturnChildren(), even if the folder is empty.
            resultContext.ReturnChildren(userFileSystemChildren.ToArray(), userFileSystemChildren.Count());

            // Save ETags, the read-only attribute and all custom columns data.
            foreach (FileSystemItemMetadataExt itemMetadata in userFileSystemChildren)
            {
                string userFileSystemItemPath = Path.Combine(UserFileSystemPath, itemMetadata.Name);
                ExternalDataManager customDataManager = Engine.ExternalDataManager(userFileSystemItemPath);

                // Save ETag on the client side, to be sent to the remote storage as part of the update.
                await customDataManager.SetCustomDataAsync(itemMetadata.ETag, itemMetadata.IsLocked, itemMetadata.CustomProperties);
            }
        }

        public async Task<IEnumerable<FileSystemItemMetadataExt>> EnumerateChildrenAsync(string pattern)
        {
            IEnumerable<FileSystemInfo> remoteStorageChildren = new DirectoryInfo(RemoteStoragePath).EnumerateFileSystemInfos(pattern);

            List<FileSystemItemMetadataExt> userFileSystemChildren = new List<FileSystemItemMetadataExt>();
            foreach (FileSystemInfo remoteStorageItem in remoteStorageChildren)
            {
                FileSystemItemMetadataExt itemInfo = Mapping.GetUserFileSysteItemMetadata(remoteStorageItem);
                userFileSystemChildren.Add(itemInfo);
            }
            return userFileSystemChildren;
        }

        /// <inheritdoc/>
        public async Task WriteAsync(IFolderMetadata folderMetadata, IOperationContext operationContext = null)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(WriteAsync)}()", UserFileSystemPath, default, operationContext);

            DirectoryInfo remoteStorageItem = new DirectoryInfo(RemoteStoragePath);

            // Update remote storage folder metadata.
            remoteStorageItem.Attributes = folderMetadata.Attributes;
            remoteStorageItem.CreationTimeUtc = folderMetadata.CreationTime.UtcDateTime;
            remoteStorageItem.LastWriteTimeUtc = folderMetadata.LastWriteTime.UtcDateTime;
            remoteStorageItem.LastAccessTimeUtc = folderMetadata.LastAccessTime.UtcDateTime;
            remoteStorageItem.LastWriteTimeUtc = folderMetadata.LastWriteTime.UtcDateTime;
        }
    }
}
