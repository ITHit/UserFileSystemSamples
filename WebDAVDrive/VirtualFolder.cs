using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;
using ITHit.WebDAV.Client;

namespace WebDAVDrive
{
    /// <summary>
    /// Represents a folder on a virtual drive. Provides methods for enumerating this folder children, 
    /// creating files and folders and updating this folder information (creatin date, modification date, attributes, etc.).
    /// </summary>
    /// <remarks>You will change methods of this class to read/write data from/to your remote storage.</remarks>
    internal class VirtualFolder : VirtualFileSystemItem, IVirtualFolder
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userfileSystemFolderPath">Path of this folder in the user file system.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFolder(string userfileSystemFolderPath, ILogger logger) 
            : base(userfileSystemFolderPath, logger)
        {

        }

        /// <summary>
        /// Gets list of files and folders in this folder.
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

            IHierarchyItemAsync[] remoteStorageChildren = null;
            // Retry the request in case the log-in dialog is shown.
            try
            {
                remoteStorageChildren = await Program.DavClient.GetChildrenAsync(new Uri(RemoteStorageUri), false);
            }
            catch (ITHit.WebDAV.Client.Exceptions.Redirect302Exception)
            {
                remoteStorageChildren = await Program.DavClient.GetChildrenAsync(new Uri(RemoteStorageUri), false);
            }

            List<FileSystemItemMetadataExt> userFileSystemChildren = new List<FileSystemItemMetadataExt>();
            foreach (IHierarchyItemAsync remoteStorageItem in remoteStorageChildren)
            {
                FileSystemItemMetadataExt itemInfo = Mapping.GetUserFileSystemItemMetadata(remoteStorageItem);
                userFileSystemChildren.Add(itemInfo);
            }

            return userFileSystemChildren;
        }

        /// <summary>
        /// Creates a new file in this folder in the remote storage.
        /// </summary>
        /// <param name="fileInfo">Information about the new file.</param>
        /// <param name="content">New file content.</param>
        /// <returns>The new ETag returned from the remote storage.</returns>
        public async Task<string> CreateFileAsync(IFileMetadata fileInfo, Stream content)
        {
            Uri newFileUri = new Uri(new Uri(RemoteStorageUri), fileInfo.Name);
            return await CreateOrUpdateFileAsync(newFileUri, fileInfo, FileMode.CreateNew, content);
        }

        /// <summary>
        /// Creates a new folder in the remote storage.
        /// </summary>
        /// <param name="folderInfo">Information about the new folder.</param>
        /// <returns>The new ETag returned from the remote storage.</returns>
        public async Task<string> CreateFolderAsync(IFolderMetadata folderInfo)
        {
            Uri newFolderUri = new Uri(new Uri(RemoteStorageUri), folderInfo.Name);
            await Program.DavClient.CreateFolderAsync(newFolderUri);
            return null; // This implementation does not support ETags on folders.
        }

        /// <summary>
        /// Updates this folder info in the remote storage.
        /// </summary>
        /// <param name="folderInfo">New folder information.</param>
        /// <param name="eTagOld">The ETag to be sent to the remote storage as part of the update request to make sure the content is not overwritten.</param>
        /// <param name="lockInfo">Information about the lock. Null is passed if the item is not locked.</param>
        /// <returns>The new ETag returned from the remote storage.</returns>
        public async Task<string> UpdateAsync(IFolderMetadata folderInfo, string eTagOld = null, ServerLockInfo lockInfo = null)
        {
            return null;
        }
    }
}
