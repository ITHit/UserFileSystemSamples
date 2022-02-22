using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;
using Client = ITHit.WebDAV.Client;

namespace WebDAVDrive
{
    /// <summary>
    /// Maps a user file system path to the remote storage path and back. 
    /// </summary>
    /// <remarks>You will change methods of this class to map the user file system path to your remote storage path.</remarks>
    internal static class Mapping // : IMapping
    {
        //private readonly VirtualEngineBase engine;

        //internal Mapping(VirtualEngineBase engine)
        //{
        //    this.engine = engine;
        //}

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
                ((FileMetadataExt)userFileSystemItem).Length = ((Client.IFile)remoteStorageItem).ContentLength;
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

            // Ser information about third-party lock if any.
            Client.LockInfo lockInfo = remoteStorageItem.ActiveLocks.FirstOrDefault();
            if (lockInfo != null)
            {
                userFileSystemItem.Lock = new ServerLockInfo()
                {
                    LockToken = lockInfo.LockToken.LockToken,
                    Owner = lockInfo.Owner,
                    Exclusive = lockInfo.LockScope == Client.LockScope.Exclusive,
                    LockExpirationDateUtc = DateTimeOffset.Now.Add(lockInfo.TimeOut)
                };
            }


            /*
            // Set custom columns to be displayed in file manager.
            // We create property definitions when registering the sync root with corresponding IDs.
            // The columns are rendered in IVirtualEngine.GetItemPropertiesAsync() call.
            userFileSystemItem.CustomProperties = ;
            */

            return userFileSystemItem;
        }

        /// <inheritdoc/>
        //public async Task<bool> IsModifiedAsync(string userFileSystemPath, FileSystemItemMetadataExt remoteStorageItemMetadata, ILogger logger)
        //{
        //    return !await engine.ExternalDataManager(userFileSystemPath, logger).ETagManager.ETagEqualsAsync(remoteStorageItemMetadata);
        //}


        /// <summary>
        /// Saves all data that is displayed in custom columns in file manager
        /// as well as any additional custom data required by the client.
        /// </summary>
        public static async Task SavePropertiesAsync(this PlaceholderItem placeholder, FileSystemItemMetadataExt metadata)
        {
            // Save lock applied by other users.
            if (metadata.Lock != null)
            {
                await placeholder.Properties.AddOrUpdateAsync("ThirdPartyLockInfo", metadata.Lock);
            }

            //foreach (FileSystemItemPropertyData prop in itemMetadata.CustomProperties)
            //{
            //    string key = ((CustomColumnIds)prop.Id).ToString();
            //    await placeholder.Properties.AddOrUpdateAsync(key, prop);
            //}
        }
    }
}
