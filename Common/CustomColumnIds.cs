using System;
using System.Collections.Generic;
using System.Text;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Represents custom columns IDs that are displayed in Windows File manager, 
    /// such as Lock Owner, Lock Scope, etc.
    /// </summary>
    public enum CustomColumnIds
    {
        /// <summary>
        /// Lock Owner column ID. The lock icon is being displayed in the Windows File Manager Status column.
        /// </summary>
        LockOwnerIcon = 2,

        /// <summary>
        /// Lock Scope column ID. Shows if the lock is Exclusive or Shared.
        /// </summary>
        LockScope = 4,

        /// <summary>
        /// Lock Expires column ID.
        /// </summary>
        LockExpirationDate = 5,

        /// <summary>
        /// ETag column ID.
        /// </summary>
        ETag = 6,
    }
}
