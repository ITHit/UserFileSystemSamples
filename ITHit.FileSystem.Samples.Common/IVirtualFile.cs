using ITHit.FileSystem;
using System.IO;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Represents a file on a virtual drive.
    /// </summary>
    /// <remarks>
    /// You will implement this interface on file items.
    /// </remarks>
    public interface IVirtualFile : IVirtualFileSystemItem
    {
        /// <summary>
        /// Reads this file content from the remote storage.
        /// </summary>
        /// <param name="offset">File content offset, in bytes, to start reading from.</param>
        /// <param name="length">Data length to read, in bytes.</param>
        /// <param name="fileSize">Total file size, in bytes.</param>
        /// <param name="resultContext">
        /// You will use this parameter to return file content by 
        /// calling <see cref="ITransferDataResultContext.ReturnData(byte[], long, long)"/>
        /// </param>
        Task ReadAsync(long offset, long length, long fileSize, ITransferDataResultContext resultContext);

        /// <summary>
        /// Updates this file in the remote storage.
        /// </summary>
        /// <param name="fileInfo">New information about the file, such as creation date, modification date, attributes, etc.</param>
        /// <param name="content">New file content or null if the file content is not modified.</param>
        /// <param name="eTagOld">The ETag to be sent to the remote storage as part of the update request to make sure the content is not overwritten.</param>
        /// <param name="lockInfo">Information about the lock. Null is passed if the item is not locked.</param>
        /// <returns>The new ETag returned from the remote storage.</returns>
        Task<string> UpdateAsync(IFileMetadata fileInfo, Stream content = null, string eTag = null, ServerLockInfo lockInfo = null);

        Task<bool> ValidateDataAsync(long offset, long length);
    }
}