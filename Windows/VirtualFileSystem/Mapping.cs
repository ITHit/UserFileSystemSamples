using System;
using System.IO;

using ITHit.FileSystem;
using ITHit.FileSystem.Synchronization;
using ITHit.FileSystem.Windows;


namespace VirtualFileSystem
{
    /// <summary>
    /// Maps a the remote storage path and data to the user file system path and data. 
    /// </summary>
    /// <remarks>
    /// You will change methods of this class to map to your own remote storage.
    /// </remarks>
    public class Mapping : IMapping
    {
        /// <summary>
        /// Remote storage root path.
        /// </summary>
        private readonly string remoteStorageRootPath;

        /// <summary>
        /// User file system root path. 
        /// </summary>
        private readonly string userFileSystemRootPath;

        /// <summary>
        /// Creates an instance of this class.
        /// </summary>
        /// <param name="userFileSystemRootPath">User file system path.</param>
        /// <param name="remoteStorageRootPath">Remote storage path.</param>
        public Mapping(string userFileSystemRootPath, string remoteStorageRootPath)
        {
            this.userFileSystemRootPath = userFileSystemRootPath;
            this.remoteStorageRootPath = remoteStorageRootPath;
        }

        /// <summary>
        /// Returns a remote storage URI that corresponds to the user file system path.
        /// </summary>
        /// <param name="userFileSystemPath">Full path in the user file system.</param>
        /// <returns>Remote storage URI that corresponds to the <paramref name="userFileSystemPath"/>.</returns>
        public string MapPath(string userFileSystemPath)
        {
            // Get path relative to the virtual root.
            string relativePath = userFileSystemPath.TrimEnd(Path.DirectorySeparatorChar).Substring(
                (userFileSystemRootPath.TrimEnd(Path.DirectorySeparatorChar)).Length);
            string path = $"{remoteStorageRootPath.TrimEnd(Path.DirectorySeparatorChar)}{relativePath}";
            return path;
        }

        /// <summary>
        /// Returns a user file system path that corresponds to the remote storage URI.
        /// </summary>
        /// <param name="remoteStorageUri">Remote storage URI.</param>
        /// <returns>Path in the user file system that corresponds to the <paramref name="remoteStorageUri"/>.</returns>
        public string ReverseMapPath(string remoteStorageUri)
        {
            // Get path relative to the virtual root.
            string relativePath = remoteStorageUri.TrimEnd(Path.DirectorySeparatorChar).Substring(
                remoteStorageRootPath.TrimEnd(Path.DirectorySeparatorChar).Length);

            string path = $"{userFileSystemRootPath.TrimEnd(Path.DirectorySeparatorChar)}{relativePath}";
            return path;
        }

        /// <summary>
        /// Gets a user file system file/folder metadata from the remote storage file/folder data.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <remarks>
        /// In your real-life file system you will change the input parameter type of this method and rewrite it
        /// to map your remote storage item data to the user file system data.
        /// </remarks>
        /// <returns>File or folder metadata that corresponds to the <paramref name="remoteStorageItem"/> parameter.</returns>
        public static IMetadata GetMetadata(FileSystemInfo remoteStorageItem)
        {
            IMetadata metadata;

            if (remoteStorageItem is FileInfo)
            {
                metadata = new FileMetadata();
                ((FileMetadata)metadata).Length = ((FileInfo)remoteStorageItem).Length;
            }
            else
            {
                metadata = new FolderMetadata();
            }

            // Typically you store your remote storage item ID in this property.
            // It will be passed to the IEngine.GetFileSystemItemAsync() method during every operation.
            // However in this sample, as soon as network path does not provide the ID, we do not set it.
            //userFileSystemItem.RemoteStorageItemId = ...;

            metadata.Name = remoteStorageItem.Name;
            metadata.Attributes = remoteStorageItem.Attributes;
            metadata.CreationTime = remoteStorageItem.CreationTime;
            metadata.LastWriteTime = remoteStorageItem.LastWriteTime;
            metadata.LastAccessTime = remoteStorageItem.LastAccessTime;
            metadata.ChangeTime = remoteStorageItem.LastWriteTime;

            return metadata;
        }
    }
}
