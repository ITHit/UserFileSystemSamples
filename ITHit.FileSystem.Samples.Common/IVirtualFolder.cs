using ITHit.FileSystem;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Represents a folder on a virtual drive.
    /// </summary>
    /// <remarks>
    /// You will implement this interface on folder items.
    /// </remarks>
    public interface IVirtualFolder : IVirtualFileSystemItem
    {
        /// <summary>
        /// Gets list of files and folders in this folder.
        /// </summary>
        /// <param name="pattern">Search pattern.</param>
        /// <returns>
        /// List of files and folders located in this folder in the remote 
        /// storage that correstonds with the provided search pattern.
        /// </returns>
        Task<IEnumerable<FileSystemItemMetadataExt>> EnumerateChildrenAsync(string pattern);

        /// <summary>
        /// Creates a new file in this folder in the remote storage.
        /// </summary>
        /// <param name="fileInfo">Information about the new file.</param>
        /// <param name="content">New file content.</param>
        /// <returns>The new ETag returned from the remote storage.</returns>
        Task<string> CreateFileAsync(IFileMetadata fileInfo, Stream content);

        /// <summary>
        /// Creates a new folder in the remote storage.
        /// </summary>
        /// <param name="folderInfo">Information about the new folder.</param>
        /// <returns>The new ETag returned from the remote storage.</returns>
        Task<string> CreateFolderAsync(IFolderMetadata folderInfo);

        /// <summary>
        /// Updates this folder info in the remote storage.
        /// </summary>
        /// <param name="folderInfo">New folder information.</param>
        /// <param name="eTagOld">The ETag to be sent to the remote storage as part of the update request to make sure the content is not overwritten.</param>
        /// <param name="lockInfo">Information about the lock. Null is passed if the item is not locked.</param>
        /// <returns>The new ETag returned from the remote storage.</returns>
        Task<string> UpdateAsync(IFolderMetadata folderInfo, string eTag = null, ServerLockInfo lockInfo = null);
    }
}
