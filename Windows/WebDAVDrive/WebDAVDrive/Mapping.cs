using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;
using Client = ITHit.WebDAV.Client;

namespace WebDAVDrive
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
            string relativePath = Path.TrimEndingDirectorySeparator(userFileSystemPath).Substring(
                Path.TrimEndingDirectorySeparator(Program.Settings.UserFileSystemRootPath).Length);
            relativePath = relativePath.TrimStart(Path.DirectorySeparatorChar);

            string[] segments = relativePath.Split('\\');

            IEnumerable<string> encodedSegments = segments.Select(x => Uri.EscapeDataString(x));
            relativePath = string.Join('/', encodedSegments);      

            string path = $"{Program.Settings.WebDAVServerUrl}{relativePath}";


            // Add trailing slash to folder URLs so Uri class concatenation works correctly.
            if(!path.EndsWith('/') && Directory.Exists(userFileSystemPath))
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
            string webDAVServerUrlPath = new UriBuilder(Program.Settings.WebDAVServerUrl).Path;

            // Get path relative to the virtual root.
            string relativePath = rsPath.Substring(webDAVServerUrlPath.TrimEnd('/').Length);
            relativePath = relativePath.TrimStart('/');

            string[] segments = relativePath.Split('/');

            IEnumerable<string> decodedSegments = segments.Select(x => Uri.UnescapeDataString(x));
            relativePath = string.Join(Path.DirectorySeparatorChar, decodedSegments);

            string path = $"{Program.Settings.UserFileSystemRootPath}{relativePath}";
            return path;
        }

        /// <summary>
        /// Returns a full remote URI with domain that corresponds to the <paramref name="relativePath"/>.
        /// </summary>
        /// <param name="relativePath">Remote storage URI.</param>
        /// <returns>Full remote URI with domain that corresponds to the <paramref name="relativePath"/>.</returns>
        public static string GetAbsoluteUri(string relativePath)
        {
            Uri webDavServerUri = new Uri(Program.Settings.WebDAVServerUrl);
            string host = webDavServerUri.GetLeftPart(UriPartial.Authority);

            string path = $"{host}/{relativePath}";
            return path;
        }


        /// <summary>
        /// Gets a user file system item info from the remote storage data.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <returns>User file system item info.</returns>
        public static FileSystemItemMetadataExt GetUserFileSystemItemMetadata(Client.IHierarchyItem remoteStorageItem)
        {
            FileSystemItemMetadataExt userFileSystemItem;

            if (remoteStorageItem is Client.IFile)
            {
                userFileSystemItem = new FileMetadataExt();
            }
            else
            {
                userFileSystemItem = new FolderMetadataExt();
            }

            userFileSystemItem.Name = remoteStorageItem.DisplayName;
            userFileSystemItem.Attributes = FileAttributes.Normal;
            userFileSystemItem.CreationTime = remoteStorageItem.CreationDate;
            userFileSystemItem.LastWriteTime = remoteStorageItem.LastModified;
            userFileSystemItem.LastAccessTime = remoteStorageItem.LastModified;
            userFileSystemItem.ChangeTime = remoteStorageItem.LastModified;

            userFileSystemItem.IsLocked = remoteStorageItem.ActiveLocks.Length > 0;

            if (remoteStorageItem is Client.IFile)
            {
                // We send the ETag to 
                // the server inside If-Match header togeter with updated content from client.
                // This will make sure the changes on the server is not overwritten.
                ((FileMetadataExt)userFileSystemItem).ETag = ((Client.IFile)remoteStorageItem).Etag;

                ((FileMetadataExt)userFileSystemItem).Length = ((Client.IFile)remoteStorageItem).ContentLength;
            };

            // Set custom columns to be displayed in file manager.
            // We create property definitions when registering the sync root with corresponding IDs.
            List<FileSystemItemPropertyData> customProps = new List<FileSystemItemPropertyData>();

            // Set lock properties.
            Client.LockInfo lockInfo = remoteStorageItem.ActiveLocks.FirstOrDefault();
            if (lockInfo != null)
            {
                ServerLockInfo serverLockInfo = new ServerLockInfo()
                {
                    LockToken = lockInfo.LockToken.LockToken,
                    Owner = lockInfo.Owner,
                    Exclusive = lockInfo.LockScope == Client.LockScope.Exclusive,
                    LockExpirationDateUtc = DateTimeOffset.Now.Add(lockInfo.TimeOut)
                };
                customProps.AddRange(
                    serverLockInfo.GetLockProperties(Path.Combine(Program.Settings.IconsFolderPath, "Locked.ico"))
                );            
            }

            // Set ETag property.
            if (remoteStorageItem is Client.IFile file)
            {
                customProps.Add(new FileSystemItemPropertyData((int)CustomColumnIds.ETag, file.Etag));
            };

            userFileSystemItem.CustomProperties = customProps;

            return userFileSystemItem;
        }
    }
}
