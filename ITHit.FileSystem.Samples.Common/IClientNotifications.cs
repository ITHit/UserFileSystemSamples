using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Represents client messages that must be to be sent to the remote storage, such as lock and unlock commands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call methods of this class when the client needs to notify the remote storage 
    /// about the changes made in the user file system.
    /// </para>
    /// <para>
    /// Methods of this interface call the <see cref="IVirtualDrive.GetVirtualFileSystemItemAsync(string, FileSystemItemTypesEnum)"/> method 
    /// and than the <see cref="IVirtualFile"/> and <see cref="IVirtualFolder"/> methods.
    /// </para>
    /// </remarks>
    public interface IClientNotifications
    {
        /// <summary>
        /// Locks the file in the remote storage. 
        /// When this method is called the <see cref="IVirtualDrive"/> calls the <see cref="IVirtualLock.LockAsync"/> method.
        /// </summary>        
        /// <param name="lockMode">Indicates automatic or manual lock.</param>
        /// <exception cref="ClientLockFailedException">
        /// Thrown when a file can not be locked. For example when a lock-token file is blocked 
        /// from another thread, during update, lock and unlock operations.
        /// </exception>
        Task LockAsync(LockMode lockMode = LockMode.Manual);

        /// <summary>
        /// Unlocks the file in the remote storage.
        /// </summary>
        Task UnlockAsync();
    }
}
