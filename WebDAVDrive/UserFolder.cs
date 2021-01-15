using ITHit.FileSystem;
using ITHit.WebDAV.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualFileSystem
{
    /// <summary>
    /// Represents a folder in the remote storage. Provides methods for enumerating this folder children, 
    /// creating files and folders and updating this folder information (creatin date, modification date, attributes, etc.).
    /// </summary>
    /// <remarks>You will change methods of this class to read/write data from/to your remote storage.</remarks>
    internal class UserFolder : UserFileSystemItem
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userfileSystemFolderPath">Path of this folder in the user file system.</param>
        /// <param name="lockInfo">Information about file lock. Pass null if the item is not locked.</param>
        public UserFolder(string userfileSystemFolderPath, LockInfo lockInfo = null) : base(userfileSystemFolderPath, lockInfo)
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
        public async Task<IEnumerable<FileSystemItemBasicInfo>> EnumerateChildrenAsync(string pattern)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests modify the IFolder.GetChildrenAsync() implementation.

            IHierarchyItemAsync[] remoteStorageChildren = await Program.DavClient.GetChildrenAsync(new Uri(RemoteStorageUri), false);

            List<FileSystemItemBasicInfo> userFileSystemChildren = new List<FileSystemItemBasicInfo>();
            foreach (IHierarchyItemAsync remoteStorageItem in remoteStorageChildren)
            {
                FileSystemItemBasicInfo itemInfo = Mapping.GetUserFileSysteItemBasicInfo(remoteStorageItem);
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
        public async Task<string> CreateFileAsync(IFileBasicInfo fileInfo, Stream content)
        {
            Uri newFileUri = new Uri(new Uri(RemoteStorageUri), fileInfo.Name);
            return await CreateOrUpdateFileAsync(newFileUri, fileInfo, FileMode.CreateNew, content);
        }

        /// <summary>
        /// Creates a folder in the remote storage.
        /// </summary>
        /// <param name="folderInfo">Information about the new folder.</param>
        /// <returns>New ETag returned from the remote storage.</returns>
        public async Task<string> CreateFolderAsync(IFolderBasicInfo folderInfo)
        {
            Uri newFolderUri = new Uri(new Uri(RemoteStorageUri), folderInfo.Name);          
            await Program.DavClient.CreateFolderAsync(newFolderUri);
            return null; // This implementation does not support ETags on folders.
        }

        /// <summary>
        /// Updates folder in the remote storage.
        /// </summary>
        /// <param name="folderInfo">New folder information.</param>
        /// <returns>New ETag returned from the remote storage.</returns>
        public async Task<string> UpdateAsync(IFolderBasicInfo folderInfo)
        {
            return null;
        }

    }
}
