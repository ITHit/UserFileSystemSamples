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
    public class VirtualFolder : VirtualFileSystemItem, IFolder
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
            string newEtag = "1234567890";

            ExternalDataManager customDataManager = Engine.CustomDataManager(userFileSystemNewItemPath);

            // Mark this item as not new, which is required for correct MS Office saving opertions.
            customDataManager.IsNew = false;

            await customDataManager.ETagManager.SetETagAsync(newEtag);

            // Update ETag in custom column displayed in file manager.
            await customDataManager.SetCustomColumnsAsync(new[] { new FileSystemItemPropertyData((int)CustomColumnIds.ETag, newEtag) });

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
            string newEtag = "1234567890";

            ExternalDataManager customDataManager = Engine.CustomDataManager(userFileSystemNewItemPath);

            customDataManager.IsNew = false;
            await customDataManager.ETagManager.SetETagAsync(newEtag);
            await customDataManager.SetCustomColumnsAsync(new[] { new FileSystemItemPropertyData((int)CustomColumnIds.ETag, newEtag) });

            return null;
        }

        /// <inheritdoc/>
        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call one of the following:
            // - resultContext.ReturnChildren() method.
            // - resultContext.ReportProgress() method.

            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChildrenAsync)}({pattern})", UserFileSystemPath);

            IEnumerable<FileSystemInfo> remoteStorageChildren = new DirectoryInfo(RemoteStoragePath).EnumerateFileSystemInfos(pattern);

            List<IFileSystemItemMetadata> userFileSystemChildren = new List<IFileSystemItemMetadata>();
            foreach (FileSystemInfo remoteStorageItem in remoteStorageChildren)
            {
                IFileSystemItemMetadata itemInfo = Mapping.GetUserFileSysteItemMetadata(remoteStorageItem);

                string userFileSystemItemPath = Path.Combine(UserFileSystemPath, itemInfo.Name);

                // Filtering existing files/folders. This is only required to avoid extra errors in the log.
                if (!FsPath.Exists(userFileSystemItemPath))
                {
                    Logger.LogMessage("Creating", userFileSystemItemPath);
                    userFileSystemChildren.Add(itemInfo);
                }

                ExternalDataManager customDataManager = Engine.CustomDataManager(userFileSystemItemPath);

                // Mark this item as not new, which is required for correct MS Office saving opertions.
                customDataManager.IsNew = false;

                // Save ETag on the client side, to be sent to the remote storage as part of the update.
                await customDataManager.ETagManager.SetETagAsync("1234567890");
            }

            // To signal that the children enumeration is completed 
            // always call ReturnChildren(), even if the folder is empty.
            resultContext.ReturnChildren(userFileSystemChildren.ToArray(), userFileSystemChildren.Count());

            // Show some custom column in file manager for demo purposes.
            foreach (IFileSystemItemMetadata itemInfo in userFileSystemChildren)
            {
                string userFileSystemItemPath = Path.Combine(UserFileSystemPath, itemInfo.Name);

                FileSystemItemPropertyData eTagColumn = new FileSystemItemPropertyData((int)CustomColumnIds.ETag, "1234567890");
                await Engine.CustomDataManager(userFileSystemItemPath).SetCustomColumnsAsync(new []{ eTagColumn });
            }
        }

        /// <inheritdoc/>
        public async Task WriteAsync(IFolderMetadata folderMetadata)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(WriteAsync)}()", UserFileSystemPath);

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
