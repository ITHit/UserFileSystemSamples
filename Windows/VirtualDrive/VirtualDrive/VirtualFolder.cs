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
        /// <param name="remoteStorageItemId">Remote storage item ID.</param>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFolder(string path, byte[] remoteStorageItemId, VirtualEngine engine, ILogger logger) : base(path, remoteStorageItemId, engine, logger)
        {

        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFileAsync(IFileMetadata fileMetadata, Stream content = null)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, fileMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}()", userFileSystemNewItemPath);

            string remoteStoragePath = Mapping.GetRemoteStoragePathById(RemoteStorageItemId);
            FileInfo remoteStorageItem = new FileInfo(Path.Combine(remoteStoragePath, fileMetadata.Name));

            // Upload file content to the remote storage.
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

            // Get ETag from your remote storage here and save it in
            // persistent placeholder properties unlil the next update.
            string eTag = (await WindowsFileSystemItem.GetUsnByPathAsync(remoteStorageItem.FullName)).ToString();
            PlaceholderItem placeholder = Engine.Placeholders.GetItem(userFileSystemNewItemPath);
            await placeholder.Properties.AddOrUpdateAsync("ETag", eTag);

            // Return remote storage item ID. It will be passed later
            // into IEngine.GetFileSystemItemAsync() method on every call.
            return WindowsFileSystemItem.GetItemIdByPath(remoteStorageItem.FullName);
        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFolderAsync(IFolderMetadata folderMetadata)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, folderMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}() Start", userFileSystemNewItemPath);

            string remoteStoragePath = Mapping.GetRemoteStoragePathById(RemoteStorageItemId);
            DirectoryInfo remoteStorageItem = new DirectoryInfo(Path.Combine(remoteStoragePath, folderMetadata.Name));
            remoteStorageItem.Create();

            // Update remote storage folder metadata.
            remoteStorageItem.Attributes = folderMetadata.Attributes;
            remoteStorageItem.CreationTimeUtc = folderMetadata.CreationTime.UtcDateTime;
            remoteStorageItem.LastWriteTimeUtc = folderMetadata.LastWriteTime.UtcDateTime;
            remoteStorageItem.LastAccessTimeUtc = folderMetadata.LastAccessTime.UtcDateTime;
            remoteStorageItem.LastWriteTimeUtc = folderMetadata.LastWriteTime.UtcDateTime;

            // Get ETag from your remote storage here and save it in persistent placeholder properties.
            string eTag = (await WindowsFileSystemItem.GetUsnByPathAsync(remoteStorageItem.FullName)).ToString();
            PlaceholderItem placeholder = Engine.Placeholders.GetItem(userFileSystemNewItemPath);
            await placeholder.Properties.AddOrUpdateAsync("ETag", eTag);

            // Return remote storage item ID. It will be passed later
            // into IEngine.GetFileSystemItemAsync() method on every call.
            return WindowsFileSystemItem.GetItemIdByPath(remoteStorageItem.FullName);
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
            }

            // To signal that the children enumeration is completed 
            // always call ReturnChildren(), even if the folder is empty.
            await resultContext.ReturnChildrenAsync(userFileSystemChildren.ToArray(), userFileSystemChildren.Count());

            // Save data that you wish to display in custom columns here.
            //foreach (FileSystemItemMetadataExt itemMetadata in userFileSystemChildren)
            //{
            //    string userFileSystemItemPath = Path.Combine(UserFileSystemPath, itemMetadata.Name);
            //    PlaceholderItem placeholder = Engine.Placeholders.GetItem(userFileSystemItemPath);
            //    await placeholder.Properties.AddOrUpdateAsync("SomeData", someData);
            //}
        }

        public async Task<IEnumerable<FileSystemItemMetadataExt>> EnumerateChildrenAsync(string pattern)
        {
            string remoteStoragePath = Mapping.GetRemoteStoragePathById(RemoteStorageItemId);
            IEnumerable<FileSystemInfo> remoteStorageChildren = new DirectoryInfo(remoteStoragePath).EnumerateFileSystemInfos(pattern);

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

            string remoteStoragePath = Mapping.GetRemoteStoragePathById(RemoteStorageItemId);
            DirectoryInfo remoteStorageItem = new DirectoryInfo(remoteStoragePath);

            // Update remote storage folder metadata.
            remoteStorageItem.Attributes = folderMetadata.Attributes;
            remoteStorageItem.CreationTimeUtc = folderMetadata.CreationTime.UtcDateTime;
            remoteStorageItem.LastWriteTimeUtc = folderMetadata.LastWriteTime.UtcDateTime;
            remoteStorageItem.LastAccessTimeUtc = folderMetadata.LastAccessTime.UtcDateTime;
            remoteStorageItem.LastWriteTimeUtc = folderMetadata.LastWriteTime.UtcDateTime;
        }
    }
}
