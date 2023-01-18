using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Client = ITHit.WebDAV.Client;
using ITHit.FileSystem;
using WebDAVCommon;
using Common.Core;

namespace WebDAVFileProviderExtension
{
    /// <summary>
    /// Maps a user file system path to the remote storage path and back. 
    /// </summary>
    /// <remarks>You will change methods of this class to map the user file system path to your remote storage path.</remarks>
    internal static class Mapping
    {
        /// <summary>
        /// Returns a remote storage URI that corresponds to the user file system path.
        /// </summary>
        /// <param name="userFileSystemPath">Full path in the user file system.</param>
        /// <returns>Remote storage URI that corresponds to the <paramref name="userFileSystemPath"/>.</returns>
        public static string MapPath(string userFileSystemPath)
        {
            // Get path relative to the virtual root.
            string relativePath = userFileSystemPath.TrimEnd(Path.DirectorySeparatorChar).Substring(
                AppGroupSettings.GetUserRootPath().TrimEnd(Path.DirectorySeparatorChar).Length);
            relativePath = relativePath.TrimStart(Path.DirectorySeparatorChar);

            string[] segments = relativePath.Split('/');

            IEnumerable<string> encodedSegments = segments.Select(x => Uri.EscapeDataString(x));
            relativePath = string.Join('/', encodedSegments);

            string path = $"{AppGroupSettings.GetWebDAVServerUrl()}{relativePath}";

            // Add trailing slash to folder URLs so Uri class concatenation works correctly.
            if (!path.EndsWith('/') && Path.GetExtension(userFileSystemPath).Length == 0)
            {
                path = $"{path}/";
            }

            return path;
        }

        /// <summary>
        /// Returns a user file system path that corresponds to the remote storage URI.
        /// </summary>
        /// <param name="remoteStorageUri">Remote storage URI.</param>
        /// <returns>Path in the user file system that corresponds to the <paramref name="remoteStorageUri"/>.</returns>
        public static string ReverseMapPath(string remoteStorageUri)
        {
            // Remove the 'https://server:8080/' part.
            string rsPath = new UriBuilder(remoteStorageUri).Path;
            string webDAVServerUrlPath = new UriBuilder(AppGroupSettings.GetWebDAVServerUrl()).Path;

            // Get path relative to the virtual root.
            string relativePath = rsPath.Substring(webDAVServerUrlPath.TrimEnd('/').Length);
            relativePath = relativePath.TrimStart('/');

            string[] segments = relativePath.Split('/');

            IEnumerable<string> decodedSegments = segments.Select(x => Uri.UnescapeDataString(x));
            relativePath = string.Join(Path.DirectorySeparatorChar, decodedSegments);

            string path = $"{AppGroupSettings.GetUserRootPath().TrimEnd(Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}{relativePath.TrimEnd('/')}";
            return path;
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
                userFileSystemItem = new FileMetadata();
                ((FileMetadata)userFileSystemItem).Length = remoteStorageFile.ContentLength;
                //userFileSystemItem.ETag = remoteStorageFile.Etag;
                userFileSystemItem.Attributes = FileAttributes.Normal;
            }
            else
            {
                userFileSystemItem = new FolderMetadata();
                userFileSystemItem.Attributes = FileAttributes.Normal | FileAttributes.Directory;
            }

            userFileSystemItem.Name = remoteStorageItem.DisplayName;
            try
            {
                userFileSystemItem.RemoteStorageItemId = Encoding.UTF8.GetBytes(ReverseMapPath(remoteStorageItem.Href.AbsoluteUri));
            }
            catch(Exception ex)
            {
                (new ConsoleLogger("Mapping")).LogError($"Error {remoteStorageItem.Href.AbsoluteUri}", ex: ex);
            }

            if (DateTime.MinValue != remoteStorageItem.CreationDate)
            {
                userFileSystemItem.CreationTime = remoteStorageItem.CreationDate;
                userFileSystemItem.LastWriteTime = remoteStorageItem.LastModified;
                userFileSystemItem.LastAccessTime = remoteStorageItem.LastModified;
                userFileSystemItem.ChangeTime = remoteStorageItem.LastModified;
            }

            // Set information about third-party lock, if any.
            Client.LockInfo lockInfo = remoteStorageItem.ActiveLocks.FirstOrDefault();
            if (lockInfo != null)
            {
                /*userFileSystemItem.Lock = new ServerLockInfo()
                {
                    LockToken = lockInfo.LockToken.LockToken,
                    Owner = lockInfo.Owner,
                    Exclusive = lockInfo.LockScope == Client.LockScope.Exclusive,
                    LockExpirationDateUtc = DateTimeOffset.Now.Add(lockInfo.TimeOut)
                };*/
            }

            /*
            // Set custom columns to be displayed in file manager.
            // We create property definitions when registering the sync root with corresponding IDs.
            // The columns are rendered in IVirtualEngine.GetItemPropertiesAsync() call.
            userFileSystemItem.CustomProperties = ;
            */

            return userFileSystemItem;
        }
    }
}
