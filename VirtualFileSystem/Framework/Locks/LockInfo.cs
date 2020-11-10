using System;
using System.Collections.Generic;
using System.Text;

namespace VirtualFileSystem
{
    /// <summary>
    /// Information about the lock returned from the remote storage as a result of the lock operation.
    /// </summary>
    public class LockInfo
    {
        /// <summary>
        /// Lock-token. Must be supplied during the item update and unlock operations.
        /// </summary>
        public string LockToken { get; set; }

        /// <summary>
        /// Lock expidation date/time returned by the server.
        /// </summary>
        public DateTimeOffset LockExpirationDateUtc { get; set; }
    }
}
