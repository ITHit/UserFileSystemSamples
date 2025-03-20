using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Synchronization;
using Client = ITHit.WebDAV.Client;


namespace WebDAVDrive
{
    /// <summary>
    /// Maps a user file system path to the remote storage path and back. 
    /// </summary>
    /// <remarks>You will change methods of this class to map the user file system path to your remote storage path.</remarks>
    public class Mapping : IMapping
    {
        /// <summary>
        /// Remote storage root path.
        /// </summary>
        private readonly string webDAVServerUrl;

        /// <summary>
        /// User file system root path. 
        /// </summary>
        private readonly string userFileSystemRootPath;

        /// <summary>
        /// Creates an instance of this class.
        /// </summary>
        /// <param name="userFileSystemRootPath">User file system path.</param>
        /// <param name="remoteStorageRootPath">Remote storage path.</param>
        public Mapping(string userFileSystemRootPath, string webDAVServerUrl)
        {
            this.userFileSystemRootPath = userFileSystemRootPath;
            this.webDAVServerUrl = webDAVServerUrl;
        }

        /// <summary>
        /// Returns a remote storage URI that corresponds to the user file system path.
        /// </summary>
        /// <param name="userFileSystemPath">Full path in the user file system.</param>
        /// <returns>Remote storage URI that corresponds to the <paramref name="userFileSystemPath"/>.</returns>
        public string MapPath(string userFileSystemPath)
        {
            // Get path relative to the virtual root.
            string relativePath = Path.TrimEndingDirectorySeparator(userFileSystemPath).Substring(
                Path.TrimEndingDirectorySeparator(userFileSystemRootPath).Length);
            relativePath = relativePath.TrimStart(Path.DirectorySeparatorChar);

            string[] segments = relativePath.Split('\\');

            IEnumerable<string> encodedSegments = segments.Select(x => Uri.EscapeDataString(x));
            relativePath = string.Join('/', encodedSegments);

            string path = $"{webDAVServerUrl.Trim('/')}/{relativePath}";

            // Add trailing slash to folder URLs so Uri class concatenation works correctly.
            if (!path.EndsWith('/') && Directory.Exists(userFileSystemPath))
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
        public string ReverseMapPath(string remoteStorageUri)
        {
            // Remove the 'https://server:8080/' part.
            string rsPath = new UriBuilder(remoteStorageUri).Path;
            string webDAVServerUrlPath = new UriBuilder(webDAVServerUrl).Path;

            // Get path relative to the virtual root.
            string relativePath = rsPath.Substring(webDAVServerUrlPath.TrimEnd('/').Length);
            relativePath = relativePath.TrimStart('/');

            string[] segments = relativePath.Split('/');

            IEnumerable<string> decodedSegments = segments.Select(x => Uri.UnescapeDataString(x));
            relativePath = string.Join(Path.DirectorySeparatorChar, decodedSegments);

            return Path.Combine(userFileSystemRootPath, relativePath);
        }

        /// <summary>
        /// Returns a full remote URI with domain that corresponds to the <paramref name="relativePath"/>.
        /// </summary>
        /// <param name="relativePath">Remote storage URI.</param>
        /// <returns>Full remote URI with domain that corresponds to the <paramref name="relativePath"/>.</returns>
        public string GetAbsoluteUri(string relativePath)
        {
            Uri webDavServerUri = new Uri(webDAVServerUrl);
            string host = webDavServerUri.GetLeftPart(UriPartial.Authority);

            string path = $"{host}/{relativePath}";
            return path;
        }


        /// <summary>
        /// Gets a user file system item info from the remote storage data.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <param name="changeType">
        /// Operation for which this metadata is requested. 
        /// If <see cref="Change.Deleted"/> is specified (in case of Sync ID algorithm), 
        /// you can fill only the remote storage item ID, the rest of the data can be omitted.
        /// </param>
        /// <returns>User file system item info.</returns>
        public static IMetadata GetMetadata(Client.IHierarchyItem remoteStorageItem, Change changeType = Change.Changed)
        {
            IMetadata metadata;

            if (remoteStorageItem is Client.IFile)
            {
                Client.IFile remoteStorageFile = (Client.IFile)remoteStorageItem;
                metadata = new FileMetadata();
                ((FileMetadata)metadata).Length = remoteStorageFile.ContentLength;
                ((FileMetadata)metadata).ContentETag = remoteStorageFile.Etag;
                metadata.Attributes = FileAttributes.Normal;
            }
            else
            {
                metadata = new FolderMetadata();
                metadata.Attributes = FileAttributes.Normal | FileAttributes.Directory;
            }

            metadata.RemoteStorageItemId = GetPropertyByteValue(remoteStorageItem, "resource-id");

            // Delete opertion requires remote storage ID only. No need to fill all other data.
            if (changeType != Change.Deleted)
            {
                metadata.RemoteStorageParentItemId = GetPropertyByteValue(remoteStorageItem, "parent-resource-id");
                metadata.MetadataETag = GetPropertyStringValue(remoteStorageItem, "metadata-Etag");
                metadata.Name = remoteStorageItem.DisplayName;
                metadata.CreationTime = remoteStorageItem.CreationDate;
                metadata.LastWriteTime = remoteStorageItem.LastModified;
                metadata.LastAccessTime = remoteStorageItem.LastModified;
                metadata.ChangeTime = remoteStorageItem.LastModified;

                // Add custom properties to metadata.Properties list.

                // Set information about lock, if any.
                Client.LockInfo lockInfo = remoteStorageItem.ActiveLocks.FirstOrDefault();
                if (lockInfo != null)
                {
                    ServerLockInfo serverLock = new ServerLockInfo()
                    {
                        LockToken = lockInfo.LockToken.LockToken,
                        Owner = lockInfo.Owner,
                        Exclusive = lockInfo.LockScope == Client.LockScope.Exclusive,
                        LockExpirationDateUtc = DateTimeOffset.Now.Add(lockInfo.TimeOut)
                    };

                    metadata.Properties.Add("LockInfo", serverLock);
                }

                metadata.Properties.PropertyChanged += PropertyChanged;
            }

            return metadata;
        }

        private static void PropertyChanged(Engine sender, PropertyChangeEventArgs e)
        {
            VirtualEngine engine = (VirtualEngine)sender;

            // Set or remove read-only flag on files locked by other users.
            if (engine.SetLockReadOnly)
            {
                if (e.Operation == PropertyOperation.Save)
                {
                    if (e.Metadata.Properties.TryGetActiveLockInfo(out var lockInfo))
                    {
                        if (!engine.IsCurrentUser(lockInfo.Owner))
                        {
                            new FileInfo(e.Path).IsReadOnly = true;
                        }
                    }
                }

                if (e.Operation == PropertyOperation.Delete)
                {
                    // If the file was locked and the lock was succesefully
                    // deleted we also remove the read-only attribute.
                    new FileInfo(e.Path).IsReadOnly = false;
                }
            }
        }

        /// <summary>
        /// Gets property byte array value. If the property does not exist, returns default value.
        /// </summary>
        private static byte[] GetPropertyByteValue(Client.IHierarchyItem remoteStorageItem, string propertyName)
        {
            byte[] resultValue = null;

            Client.Property property = remoteStorageItem.Properties.Where(p => p.Name.Name == propertyName).FirstOrDefault();
            if (property != null)
            {
                resultValue = Encoding.UTF8.GetBytes(property.StringValue);
            }

            return resultValue;
        }

        /// <summary>
        /// Gets property string value. If the property does not exist, returns default value.
        /// </summary>
        private static string GetPropertyStringValue(Client.IHierarchyItem remoteStorageItem, string propertyName)
        {
            string resultValue = null;

            Client.Property property = remoteStorageItem.Properties.Where(p => p.Name.Name == propertyName).FirstOrDefault();
            if (property != null)
            {
                resultValue = property.StringValue;
            }

            return resultValue;
        }


        /// <summary>
        /// Gets properties to be returned with each item when listing 
        /// folder content or getting an item from server.
        /// </summary>
        /// <returns>List of properties.</returns>
        public static Client.PropertyName[] GetDavProperties()
        {
            Client.PropertyName[] propNames = new Client.PropertyName[4];
            propNames[0] = new Client.PropertyName("resource-id", "DAV:");          // Remote storage item ID
            propNames[1] = new Client.PropertyName("parent-resource-id", "DAV:");   // Parent remote storage item ID
            propNames[2] = new Client.PropertyName("Etag", "DAV:");                 // Content eTag.
            propNames[3] = new Client.PropertyName("metadata-Etag", "DAV:");        // Metadata eTag.

            return propNames;
        }
    }
}
