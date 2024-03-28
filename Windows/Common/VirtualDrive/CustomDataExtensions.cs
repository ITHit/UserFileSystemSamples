using System;
using System.IO;
using System.Threading.Tasks;

using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Windows.ExternalDataManager;


namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Provides methods for getting and setting custom data associated with a file or folder.
    /// </summary>
    public static class CustomDataExtensions
    {
        /// <summary>
        /// Saves all custom metadata properties, such as locks, to storage associated with an item.
        /// This data that is displayed in custom columns in file manager.
        /// </summary>
        /// <param name="properties">Custom data attached to the item.</param>
        /// <param name="metadata">Remote storage item metadata.</param>
        public static void SaveProperties(this ICustomData properties, FileSystemItemMetadataExt metadata)
        {
            ICustomDataWindows propertiesWindows = properties as ICustomDataWindows;
            string path = propertiesWindows.Placeholder.Path;
            VirtualEngineBase engine =  propertiesWindows.Placeholder.Engine as VirtualEngineBase;

            // Save or delete lock.
            if (metadata.Lock != null)
            {
                properties.SetLockInfo(metadata.Lock);

                if (!properties.GetEngine().IsCurrentUser(metadata.Lock.Owner))
                {
                    if (engine.SetLockReadOnly)
                    {
                        new FileInfo(path).IsReadOnly = true;
                    }
                }
            }
            else
            {
                if(properties.TryDeleteLockInfo())
                {
                    // If the file was locked and the lock was succesefully
                    // deleted we also remove the read-only attribute.
                    if (engine.SetLockReadOnly)
                    {
                        new FileInfo(path).IsReadOnly = false;
                    }
                }
            }
        }

        /// <summary>
        /// Tries to get lock info.
        /// </summary>
        /// <param name="properties">Custom data attached to the item.</param>
        /// <param name="serverLockInfo">Lock info.</param>
        /// <returns>True if method succeeded and the item is locked. False if the method failed or the item is not locked.</returns>
        public static bool TryGetLockInfo(this ICustomData properties, out ServerLockInfo serverLockInfo)
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
            if (properties.TryGetLockInfo(out ServerLockInfo lockInfo))
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
