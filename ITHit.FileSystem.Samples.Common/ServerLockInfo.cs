using System;
using System.Collections.Generic;
using System.Text;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Information about the lock returned from the remote storage as a result of the lock operation.
    /// </summary>
    public class ServerLockInfo
    {
        /// <summary>
        /// Lock-token. Must be supplied during the item update and unlock operations.
        /// </summary>
        public string LockToken { get; set; }

        /// <summary>
        /// Lock expidation date/time returned by the server.
        /// </summary>
        public DateTimeOffset LockExpirationDateUtc { get; set; }

        /// <summary>
        /// Name of the user that locked the item.
        /// </summary>
        public string Owner { get; set; }

        /// <summary>
        /// True if the item is locked exclusively. False in case the item has a shared lock.
        /// </summary>
        public bool Exclusive { get; set; }

        /// <summary>
        /// Gets this lock info as a set of properties that can be visually displayed in file manager.
        /// </summary>
        /// <param name="lockIconPath">Lock icon path that will be displayed in file manager.</param>
        /// <returns>List of properties.</returns>
        public IEnumerable<FileSystemItemPropertyData> GetLockProperties(string lockIconPath)
        {
            List<FileSystemItemPropertyData> lockProps = new List<FileSystemItemPropertyData>();
            lockProps.Add(new FileSystemItemPropertyData((int)CustomColumnIds.LockOwnerIcon,        Owner,                              lockIconPath));
            lockProps.Add(new FileSystemItemPropertyData((int)CustomColumnIds.LockScope,            Exclusive ? "Exclusive" : "Shared"));
            lockProps.Add(new FileSystemItemPropertyData((int)CustomColumnIds.LockExpirationDate,   LockExpirationDateUtc.ToString()));
            return lockProps;
        }
    }
}
