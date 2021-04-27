using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Provides methods for synching the user file system to the remote storage.
    /// </summary>
    internal interface IRemoteStorageRawItem : IClientNotifications
    {
        /// <summary>
        /// Creates the file or folder in the remote storage.
        /// </summary>
        Task CreateAsync();

        /// <summary>
        /// Sends content to the remote storage if the item is modified. 
        /// If Auto-locking is enabled, automatically locks the file if not locked. 
        /// Unlocks the file after the update if auto-locked.
        /// </summary>
        Task UpdateAsync();

        /// <summary>
        /// Moves the item in the remote storage.  
        /// Calls <see cref="MoveToAsync(string, IConfirmationResultContext)"/> method and than <see cref="MoveToCompletionAsync"/>.
        /// </summary>
        /// <param name="userFileSystemNewPath">Target path in the user file system.</param>
        Task MoveToAsync(string userFileSystemNewPath);

        /// <summary>
        /// Deletes file or folder in the remote storage.
        /// </summary>
        Task<bool> DeleteAsync();
    }
}
