using System;
using System.IO;
using System.Threading.Tasks;

using ITHit.FileSystem.Windows;


namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Provides methods for getting and setting custom data associated with a file or folder.
    /// </summary>
    public static class CustomDataExtensions
    {
        /// <summary>
        /// Tries to get lock info.
        /// </summary>
        /// <remarks>This method returns only currently active lock. It does NOT return expired lock.</remarks>
        /// <param name="properties">Custom data attached to the item.</param>
        /// <param name="serverLockInfo">Lock info.</param>
        /// <returns>True if method succeeded and the item is locked. False if the method failed or the item is not locked.</returns>
        public static bool TryGetActiveLockInfo(this IPropertiesDictionary properties, out ServerLockInfo serverLockInfo)
        {
            if (properties.TryGetValue<ServerLockInfo>("LockInfo", out ServerLockInfo lockInfo))
            {
                if (lockInfo.LockExpirationDateUtc > DateTimeOffset.Now)
                {
                    serverLockInfo = lockInfo;
                    return true;
                }
            }
            serverLockInfo = null;
            return false;
        }

        /// <summary>
        /// Tries to get lock info.
        /// </summary>
        /// <param name="properties">Custom data attached to the item.</param>
        /// <param name="serverLockInfo">Lock info.</param>
        /// <returns>True if method succeeded and the item is locked. False if the method failed or the item is not locked.</returns>
        public static bool TryGetActiveLockInfo(this ICustomData properties, out ServerLockInfo serverLockInfo)
        {
            if (properties.TryGetValue("LockInfo", out IDataItem propLockInfo))
            {
                if (propLockInfo.TryGetValue<ServerLockInfo>(out ServerLockInfo lockInfo))
                {
                    if (lockInfo.LockExpirationDateUtc > DateTimeOffset.Now)
                    {
                        serverLockInfo = lockInfo;
                        return true;
                    }
                }
            }
            serverLockInfo = null;
            return false;
        }

        /// <summary>
        /// Tries to get lock token for the current user.
        /// </summary>
        /// <param name="properties">Custom data attached to the item.</param>
        /// <param name="lockToken">Lock token.</param>
        /// <returns>True if method succeeded and the item is locked by current user. False if the method failed or the item is not locked by current user.</returns>
        public static bool TryGetCurrentUserLockToken(this ICustomData properties, out string lockToken)
        {
            if (properties.TryGetActiveLockInfo(out ServerLockInfo lockInfo))
            {
                if (properties.GetEngine().IsCurrentUser(lockInfo.Owner))
                {
                    lockToken = lockInfo.LockToken;
                    return true;
                }
            }
            lockToken = null;
            return false;
        }

        /// <summary>
        /// Sets lock info.
        /// </summary>
        /// <param name="properties">Custom data attached to the item.</param>
        /// <param name="serverLockInfo">Lock info.</param>
        public static void SetLockInfo(this ICustomData properties, ServerLockInfo serverLockInfo)
        {
            properties.AddOrUpdate("LockInfo", serverLockInfo);
        }

        /// <summary>
        /// Tries to delete lock info.
        /// </summary>
        /// <param name="placeholder">User file system placeholder item.</param>
        /// <returns>True if method succeeded. False - otherwise.</returns>
        public static bool TryDeleteLockInfo(this ICustomData properties)
        {
            return properties.Remove("LockInfo");
        }

        /// <summary>
        /// Gets Engine instance.
        /// </summary>
        /// <param name="properties">Custom data attached to the item.</param>
        /// <returns>Engine</returns>
        private static VirtualEngineBase GetEngine(this ICustomData properties)
        {
            return (properties as ICustomDataWindows).Placeholder.Engine as VirtualEngineBase;
        }
    }
}
