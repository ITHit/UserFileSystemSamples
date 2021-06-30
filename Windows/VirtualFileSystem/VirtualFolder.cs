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

namespace VirtualFileSystem
{
    
    /// <inheritdoc cref="IFolder"/>
    public class VirtualFolder : VirtualFileSystemItem, IFolder
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="path">Folder path in the user file system.</param>
        /// <param name="itemId">Remote storage item ID.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFolder(string path, byte[] itemId, ILogger logger) : base(path, itemId, logger)
        {

        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFileAsync(IFileMetadata fileMetadata, Stream content = null)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}()", Path.Combine(UserFileSystemPath, fileMetadata.Name));

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

            // Return remote storage item ID. It will be passed later into IEngine.GetFileSystemItemAsync() method.
            return WindowsFileSystemItem.GetItemIdByPath(remoteStorageItem.FullName); 
        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFolderAsync(IFolderMetadata folderMetadata)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}()", Path.Combine(UserFileSystemPath, folderMetadata.Name));

            DirectoryInfo remoteStorageItem = new DirectoryInfo(Path.Combine(RemoteStoragePath, folderMetadata.Name));
            remoteStorageItem.Create();

            // Update remote storage folder metadata.
            remoteStorageItem.Attributes = folderMetadata.Attributes;
            remoteStorageItem.CreationTimeUtc = folderMetadata.CreationTime.UtcDateTime;
            remoteStorageItem.LastWriteTimeUtc = folderMetadata.LastWriteTime.UtcDateTime;
            remoteStorageItem.LastAccessTimeUtc = folderMetadata.LastAccessTime.UtcDateTime;
            remoteStorageItem.LastWriteTimeUtc = folderMetadata.LastWriteTime.UtcDateTime;

            // Return remote storage item ID. It will be passed later into IEngine.GetFileSystemItemAsync() method.
            return WindowsFileSystemItem.GetItemIdByPath(remoteStorageItem.FullName);
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
            }

            // To signal that the children enumeration is completed 
            // always call ReturnChildren(), even if the folder is empty.
            resultContext.ReturnChildren(userFileSystemChildren.ToArray(), userFileSystemChildren.Count());
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
