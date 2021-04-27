using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;

namespace VirtualFileSystem
{
    /// <summary>
    /// Represents a folder in the remote storage. Provides methods for enumerating this folder children, 
    /// creating files and folders and updating this folder information (creatin date, modification date, attributes, etc.).
    /// </summary>
    /// <remarks>You will change methods of this class to read/write data from/to your remote storage.</remarks>
    internal class VirtualFolder : VirtualFileSystemItem, IVirtualFolder
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userfileSystemFolderPath">Path of this folder in the user file system.</param>
        /// <param name="virtualDrive">Virtual Drive instance that created this item.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFolder(string userfileSystemFolderPath, VirtualDrive virtualDrive, ILogger logger) 
            : base(userfileSystemFolderPath, virtualDrive, logger)
        {

        }

        /// <summary>
        /// Gets list of files and folders in this folder in the remote storage.
        /// </summary>
        /// <param name="pattern">Search pattern.</param>
        /// <returns>
        /// List of files and folders located in this folder in the remote 
        /// storage that correstonds with the provided search pattern.
        /// </returns>
        public async Task<IEnumerable<FileSystemItemMetadataExt>> EnumerateChildrenAsync(string pattern)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests modify the IFolder.GetChildrenAsync() implementation.

            IEnumerable<FileSystemInfo> remoteStorageChildren = new DirectoryInfo(RemoteStoragePath).EnumerateFileSystemInfos(pattern);

            List<FileSystemItemMetadataExt> userFileSystemChildren = new List<FileSystemItemMetadataExt>();
            foreach (FileSystemInfo remoteStorageItem in remoteStorageChildren)
            {
                FileSystemItemMetadataExt itemInfo = Mapping.GetUserFileSysteItemMetadata(remoteStorageItem);
                userFileSystemChildren.Add(itemInfo);
            }

            return userFileSystemChildren;
        }

        /// <summary>
        /// Creates a file in the remote storage.
        /// </summary>
        /// <param name="fileInfo">Information about the new file.</param>
        /// <param name="content">New file content.</param>
        /// <returns>New ETag returned from the remote storage.</returns>
        public async Task<string> CreateFileAsync(IFileMetadata fileInfo, Stream content)
        {
            string itemPath = Path.Combine(RemoteStoragePath, fileInfo.Name);
            return await CreateOrUpdateFileAsync(itemPath, fileInfo, FileMode.CreateNew, content);
        }

        /// <summary>
        /// Creates a folder in the remote storage.
        /// </summary>
        /// <param name="folderInfo">Information about the new folder.</param>
        /// <returns>New ETag returned from the remote storage.</returns>
        public async Task<string> CreateFolderAsync(IFolderMetadata folderInfo)
        {
            string itemPath = Path.Combine(RemoteStoragePath, folderInfo.Name);
            return await CreateOrUpdateFolderAsync(itemPath, folderInfo, FileMode.CreateNew);
        }

        /// <summary>
        /// Updates folder in the remote storage.
        /// </summary>
        /// <param name="folderInfo">New folder information.</param>
        /// <param name="eTagOld">The ETag to be sent to the remote storage as part of the update request to make sure the content is not overwritten.</param>
        /// <param name="lockInfo">Information about the lock. Null if the item is not locked.</param>
        /// <returns>The new ETag returned from the remote storage.</returns>
        public async Task<string> UpdateAsync(IFolderMetadata folderInfo, string eTagOld = null, ServerLockInfo lockInfo = null)
        {
            return await CreateOrUpdateFolderAsync(RemoteStoragePath, folderInfo, FileMode.Open, eTagOld, lockInfo);
        }

    }
}
