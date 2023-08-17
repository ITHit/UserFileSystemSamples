using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ITHit.FileSystem.Windows;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Provides methods for getting and setting custom data associated with a file or folder.
    /// </summary>
    public static class PlaceholderItemExtensions
    {
        /// <summary>
        /// Returns true if the remote item is modified. False - otherwise.
        /// </summary>
        /// <remarks>
        /// This method compares client and server eTags and returns true if the 
        /// item in the user file system must be updated with the data from the remote storage.
        /// </remarks>
        /// <param name="placeholder">User file system placeholder item.</param>
        /// <param name="remoteStorageItem">Remote storage item metadata.</param>
        /// <returns></returns>
        public static async Task<bool> IsModifiedAsync(this PlaceholderItem placeholder, FileSystemItemMetadataExt remoteStorageItemMetadata)
        {
            placeholder.TryGetETag(out string clientEtag);
            return clientEtag != remoteStorageItemMetadata.ETag;
        }


        /// <summary>
        /// Saves all data that is displayed in custom columns in file manager
        /// as well as any additional custom data required by the client.
        /// </summary>
        /// <param name="placeholder">User file system placeholder item.</param>
        /// <param name="metadata">Remote storage item metadata.</param>
        /// <returns>A task object that can be awaited.</returns>
        public static async Task SavePropertiesAsync(this PlaceholderItem placeholder, FileSystemItemMetadataExt metadata)
        {
            // Save or remore lock.
            if (metadata.Lock != null)
            {
                placeholder.SetLockInfo(metadata.Lock);
            }
            else
            {
                placeholder.TryDeleteLockInfo();
            }

            // Save eTag.
            // Update eTag only for offline files. For online files eTag is updated in IFile.ReadAsync.
            if (metadata.ETag != null && System.IO.File.GetAttributes(placeholder.Path).HasFlag(System.IO.FileAttributes.Offline))
            {
                placeholder.SetETag(metadata.ETag);
            }

            //foreach (FileSystemItemPropertyData prop in metadata.CustomProperties)
            //{
            //    string key = ((CustomColumnIds)prop.Id).ToString();
            //    await placeholder.Properties.AddOrUpdateAsync(key, prop);
            //}
        }

        /// <summary>
        /// Tries to get eTag.
        /// </summary>
        /// <param name="placeholder">User file system placeholder item.</param>
        /// <param name="eTag">eTag.</param>
        /// <returns>True if method succeeded. False - otherwise.</returns>
        public static bool TryGetETag(this PlaceholderItem placeholder, out string eTag)
        {
            if (placeholder.Properties.TryGetValue("ETag", out IDataItem propETag))
            {
                return propETag.TryGetValue<string>(out eTag);
            }
            eTag = null;
            return false;
        }

        /// <summary>
        /// Sets eTag.
        /// </summary>
        /// <param name="placeholder">User file system placeholder item.</param>
        /// <param name="eTag">eTag.</param>
        public static void SetETag(this PlaceholderItem placeholder, string eTag)
        {
            placeholder.Properties.AddOrUpdate("ETag", eTag);
        }

        /// <summary>
        /// Tries to get lock info.
        /// </summary>
        /// <param name="placeholder">User file system placeholder item.</param>
        /// <param name="serverLockInfo">Lock info.</param>
        /// <returns>True if method succeeded and the item is locked. False if the method failed or the item is not locked.</returns>
        public static bool TryGetLockInfo(this PlaceholderItem placeholder, out ServerLockInfo serverLockInfo)
        {
            if (placeholder.Properties.TryGetValue("LockInfo", out IDataItem propLockInfo))
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
        /// Sets lock info.
        /// </summary>
        /// <param name="placeholder">User file system placeholder item.</param>
        /// <param name="serverLockInfo">Lock info.</param>
        public static void SetLockInfo(this PlaceholderItem placeholder, ServerLockInfo serverLockInfo)
        {
            placeholder.Properties.AddOrUpdate("LockInfo", serverLockInfo);
        }

        /// <summary>
        /// Tries to delete lock info.
        /// </summary>
        /// <param name="placeholder">User file system placeholder item.</param>
        /// <returns>True if method succeeded. False - otherwise.</returns>
        public static bool TryDeleteLockInfo(this PlaceholderItem placeholder)
        {
            return placeholder.Properties.Remove("LockInfo");
        }
    }
}
