using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common
{

    /// <summary>
    /// Represents messages sent from the remote storage to the virtual drive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call methods of this class when the client receives messages from the remote storage 
    /// (for example via we sockets) about changes in the remote storage, such as files and folders creation, update, 
    /// deletion, move/rename, etc.
    /// </para>
    /// </remarks>
    public interface IServerNotifications
    {
        /// <summary>
        /// Creates a new file or folder on this virtual drive.
        /// </summary>
        /// <param name="newItemsInfo">Array of new files and folders.</param>
        /// <remarks>
        /// <para>Call this method from your remote storage monitor when new items are created in the remote storage.</para>
        /// <para>
        /// Because of the on-demand loading, the folder specified in <paramref name="userFileSystemParentPath"/> may not exist in the user file system. 
        /// In this case the new items will not be created and this method method will return 0.
        /// </para>
        /// </remarks>
        /// <returns>Number of items created.</returns>
        Task<uint> CreateAsync(FileSystemItemMetadata[] newItemsInfo);

        /// <summary>
        /// Updates a file or folder on this virtual drive. 
        /// This method automatically hydrates and dehydrate files.
        /// </summary>
        /// <remarks>
        /// <para>Call this method from your remote storage monitor when a file or folder is updated in the remote storage.</para>
        /// <para>This method failes if the file or folder in user file system is modified (not in-sync with the remote storage).</para>
        /// <para>
        /// Because of the on-demand loading, the item specified in <paramref name="userFileSystemPath"/> may not exist in the user file system. 
        /// In this case the item will not be updated and this method will return false.
        /// </para>
        /// </remarks>
        /// <param name="itemInfo">New file or folder info.</param>
        /// <returns>True if the file was updated. False - otherwise, for example if the item does not exist in the user file system.</returns>
        Task<bool> UpdateAsync(FileSystemItemMetadata itemInfo);

        /// <summary>
        /// Deletes a file or folder from this virtual drive.
        /// </summary>
        /// <remarks>
        /// <para>Call this method from your remote storage monitor when a file or folder is deleted in the remote storage.</para>
        /// <para>
        /// This method throws <see cref="ConflictException"/> if the file or folder or any file or folder 
        /// in the folder hierarchy being deleted in user file system is modified (not in sync with the remote storage).
        /// </para>
        /// <para>
        /// Because of the on-demand loading, the item specified in <paramref name="userFileSystemPath"/> may not exist in the user file system. 
        /// In this case the item will not be deleted and this method will return false.
        /// </para>
        /// </remarks>
        /// <returns>True if the file was deleted. False - otherwise, for example if the item does not exist in the user file system.</returns>
        Task<bool> DeleteAsync();

        /// <summary>
        /// Moves a file or folder on this virtual drive.
        /// </summary>
        /// <param name="userFileSystemNewPath">New path in user file system.</param>
        /// <remarks>
        /// <para>Call this method from your remote storage monitor when a file or folder is moved in the remote storage.</para>
        /// <para>
        /// This method failes if the file or folder in user file system is modified (not in sync with the remote storage)
        /// or if the target file exists.
        /// </para>
        /// <para>
        /// Because of the on-demand loading, the item that is being moved may not exist in the user file system. 
        /// In this case the item will not be moved and this method will return false.
        /// </para>
        /// </remarks>
        /// <returns>True if the file was moved. False - otherwise, for example if the item does not exist in the user file system.</returns>
        Task<bool> MoveToAsync(string userFileSystemNewPath);

        /// <summary>
        /// Sets or removes read-only attribute on files on this virtual drive.
        /// </summary>
        /// <param name="set">True to set the read-only attribute. False - to remove the read-only attribute.</param>
        //Task<bool> SetLockedByAnotherUserAsync(string userFileSystemPath, bool set);
    }
}
