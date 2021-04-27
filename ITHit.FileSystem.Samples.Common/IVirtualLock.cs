using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Represents file of folder that can be locked in the remote storage. 
    /// Implementing this interface on an item will make the lock icon to appear in file manager.
    /// </summary>
    public interface IVirtualLock
    {
        /// <summary>
        /// Locks this item in the remote storage.
        /// </summary>
        /// <returns>Lock info that conains lock-token returned by the remote storage.</returns>
        /// <remarks>
        /// Lock your item in the remote storage in this method and receive the lock-token.
        /// Return a new <see cref="ServerLockInfo"/> object with the <see cref="ServerLockInfo.LockToken"/> being set from this function.
        /// The <see cref="ServerLockInfo"/> will become available via <see cref="IVirtualFile.UpdateAsync"/> and <see cref="IVirtualFolder.UpdateAsync"/> 
        /// methods lockInfo parameter when the item in the remote storage should be updated. 
        /// Supply the lock-token as part of your server update request.
        /// </remarks>
        Task<ServerLockInfo> LockAsync();

        /// <summary>
        /// Unlocks this item in the remote storage.
        /// </summary>
        /// <param name="lockToken">Lock token to unlock the item in the remote storage.</param>
        /// <remarks>
        /// Unlock your item in the remote storage in this method using the 
        /// <paramref name="lockToken"/> parameter.
        /// </remarks>
        Task UnlockAsync(string lockToken);
    }
}
