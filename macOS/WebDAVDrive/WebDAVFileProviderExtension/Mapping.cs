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
using System.Security.Policy;
using FileProvider;

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
            IFileSystemItemMetadataMac userFileSystemItem;

            if (remoteStorageItem is Client.IFile)
            {
                Client.IFile remoteStorageFile = (Client.IFile)remoteStorageItem;
                userFileSystemItem = new FileMetadataMac();
                ((FileMetadataMac)userFileSystemItem).Length = remoteStorageFile.ContentLength;
                userFileSystemItem.Attributes = FileAttributes.Normal;

                // Set etag.
                ((FileMetadataMac)userFileSystemItem).ContentETag = remoteStorageFile.Etag;
                userFileSystemItem.Properties.AddOrUpdate("eTag", remoteStorageFile.Etag);
            }
            else
            {
                userFileSystemItem = new FolderMetadataMac();
                userFileSystemItem.Attributes = FileAttributes.Normal | FileAttributes.Directory;
            }

            userFileSystemItem.Name = remoteStorageItem.DisplayName;
            userFileSystemItem.RemoteStorageItemId = Encoding.UTF8.GetBytes(GetPropertyValue(remoteStorageItem, "resource-id", remoteStorageItem.Href.AbsoluteUri));
            userFileSystemItem.RemoteStorageParentItemId = Encoding.UTF8.GetBytes(GetPropertyValue(remoteStorageItem, "parent-resource-id",
                remoteStorageItem.Href.AbsoluteUri.Remove(remoteStorageItem.Href.AbsoluteUri.Length - remoteStorageItem.Href.Segments.Last().Length)));
            userFileSystemItem.MetadataETag = GetPropertyValue(remoteStorageItem, "metadata-Etag", null);

            // Set item capabilities.
            userFileSystemItem.Capabilities = FileSystemItemCapabilityMac.Writing
               | FileSystemItemCapabilityMac.Deleting
               | FileSystemItemCapabilityMac.Reading
               | FileSystemItemCapabilityMac.Renaming
               | FileSystemItemCapabilityMac.Reparenting
               | FileSystemItemCapabilityMac.ExcludingFromSync;

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

                if(lockInfo.Owner != Environment.UserName)
                {
                    // Set readOnly attributes when a file is locked by another user.
                    userFileSystemItem.Attributes |= FileAttributes.ReadOnly;
                }
            }

            return userFileSystemItem;
        }

        /// <summary>
        /// Returns property value, if property not exists returns default value.
        /// </summary>
        private static string GetPropertyValue(Client.IHierarchyItem remoteStorageItem, string propertyName, string defaultValue)
        {
            string resultValue = null;

            Client.Property property = remoteStorageItem.Properties.Where(p => p.Name.Name == propertyName).FirstOrDefault();
            if (property != null)
            {
                resultValue = property.StringValue;
            }
            else if (defaultValue != null)
            {
                resultValue = defaultValue;
            }

            return resultValue;
        }
    }
}
