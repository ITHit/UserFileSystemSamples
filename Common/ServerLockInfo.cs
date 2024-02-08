using System;


namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Information about the lock.
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
        public bool Exclusive { get; set; } = true;

        /// <summary>
        /// Lock mode.
        /// </summary>
        /// <remarks>
        /// If the item is locked by this user on this machine the value of this property indicates automatic or manual lock.
        /// If the item is locked by other user or locked by this user on other machine, it contains <see cref="LockMode.None"/> value.
        /// </remarks>
        public LockMode Mode { get; set; } = LockMode.None;
    }
}
