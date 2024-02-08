using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Client = ITHit.WebDAV.Client;
using ITHit.FileSystem;
using WebDAVCommon;
using Common.Core;
using ITHit.FileSystem.Mac;
using ITHit.WebDAV.Client;

namespace WebDAVFileProviderExtension
{
    /// <summary>
    /// Maps a user file system path to the remote storage path and back. 
    /// </summary>
    /// <remarks>You will change methods of this class to map the user file system path to your remote storage path.</remarks>
    internal static class Mapping
    {
        /// <summary>
        /// Returns Uri by storage item Id.
        /// </summary>
        /// <param name="remoteStorageUriById">Id uri on the WebDav server.</param>
        /// <returns>Uri on Webdav server.</returns>
        public static Uri GetUriById(byte[] remoteStorageItemId, string webDAVServerRootUrl)
        {
            string remoteStorageIdStr = Encoding.UTF8.GetString(remoteStorageItemId);
            return Uri.IsWellFormedUriString(remoteStorageIdStr, UriKind.Absolute) ? new Uri(remoteStorageIdStr) :
                new Uri(webDAVServerRootUrl.TrimEnd('/') + "/" + remoteStorageIdStr.TrimStart('/'));
        }

        /// <summary>
        /// Gets a user file system item info from the remote storage data.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <returns>User file system item info.</returns>
        public static IFileSystemItemMetadata GetUserFileSystemItemMetadata(Client.IHierarchyItem remoteStorageItem)
        {
            IFileSystemItemMetadata userFileSystemItem;

            if (remoteStorageItem is Client.IFile)
            {
                Client.IFile remoteStorageFile = (Client.IFile)remoteStorageItem;
                userFileSystemItem = new FileMetadataMac();
                ((FileMetadataMac)userFileSystemItem).Length = remoteStorageFile.ContentLength;
                userFileSystemItem.Attributes = FileAttributes.Normal;

                // Set etag.
                userFileSystemItem.Properties.AddOrUpdate("eTag", remoteStorageFile.Etag);
            }
            else
            {
                userFileSystemItem = new FolderMetadataMac();
                userFileSystemItem.Attributes = FileAttributes.Normal | FileAttributes.Directory;
            }

            userFileSystemItem.Name = remoteStorageItem.DisplayName;
            userFileSystemItem.RemoteStorageItemId = GetPropertyValue(remoteStorageItem, "resource-id", remoteStorageItem.Href.AbsoluteUri);
            userFileSystemItem.RemoteStorageParentItemId = GetPropertyValue(remoteStorageItem, "parent-resource-id", null);

            if (DateTime.MinValue != remoteStorageItem.CreationDate)
            { 
                DateTimeOffset lastModifiedDate = remoteStorageItem.LastModified;
                userFileSystemItem.CreationTime = remoteStorageItem.CreationDate;
                userFileSystemItem.LastWriteTime = lastModifiedDate;
                userFileSystemItem.LastAccessTime = remoteStorageItem.LastModified;
                userFileSystemItem.ChangeTime = lastModifiedDate;
            }

            // Set information about third-party lock, if any.            
            Client.LockInfo lockInfo = remoteStorageItem.ActiveLocks.FirstOrDefault();
            if (lockInfo != null)
            {
                IFileSystemItemMetadataMac userFileSystemItemMac = userFileSystemItem as IFileSystemItemMetadataMac;
                userFileSystemItem.Properties.AddOrUpdate("LockToken", new ServerLockInfo()
                {
                    LockToken = lockInfo.LockToken.LockToken,
                    Owner = lockInfo.Owner,
                    Exclusive = lockInfo.LockScope == Client.LockScope.Exclusive,
                    LockExpirationDateUtc = DateTimeOffset.Now.Add(lockInfo.TimeOut)
                });

                // Display system lock icon for the file.
                userFileSystemItemMac.Decorations.Add("com.webdav.vfs.app.extension.decorating.locked");

                // Add Unclock context menu for the item in macOS finder.
                userFileSystemItemMac.UserInfo.AddOrUpdate("locked", "1");
            }       

            return userFileSystemItem;
        }

        /// <summary>
        /// Returns property value, if property not exists returns default value.
        /// </summary>
        private static byte[] GetPropertyValue(Client.IHierarchyItem remoteStorageItem, string propertyName, string defaultValue)
        {
            byte[] resultValue = null;
            try
            {
                Client.Property property = remoteStorageItem.Properties.Where(p => p.Name.Name == propertyName).FirstOrDefault();
                if (property != null)
                {
                    resultValue = Encoding.UTF8.GetBytes(property.StringValue);
                }
                else if(defaultValue != null)
                {
                    resultValue = Encoding.UTF8.GetBytes(defaultValue);
                }

            }
            catch (Exception ex)
            {
                (new ConsoleLogger("Mapping")).LogError($"Error parsing {remoteStorageItem.Href.AbsoluteUri} property {propertyName}", ex: ex);
            }

            return resultValue;
        }
    }
}
