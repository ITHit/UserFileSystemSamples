using System;
using System.IO;
using System.Text;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;

namespace FileProviderExtension
{
    /// <summary>
    /// Maps a user file system path to the remote storage path and back. 
    /// </summary>
    /// <remarks>You will change methods of this class to map the user file system path to your remote storage path.</remarks>
    internal static class Mapping
    {
        /// <summary>
        /// Encodes remote file/folder path to array of bytes.
        /// </summary>
        /// <param name="remoteStoragePath">Full path in the remote file system.</param>
        /// <returns>Array of bytes.</returns>
        public static byte[] EncodePath(string remoteStoragePath)
        {
            return Encoding.UTF8.GetBytes(remoteStoragePath);
        }

        /// <summary>
        /// Decodes array of bytes to remote file/folder path.
        /// </summary>
        /// <param name="remoteStorageItemId">Remote storage Item Id.</param>
        /// <returns>Remote storage path.</returns>
        public static string DecodePath(byte[] remoteStorageItemId)
        {
            return Encoding.UTF8.GetString(remoteStorageItemId);
        }

        /// <summary>
        /// Gets a user file system item info from the remote storage data.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <returns>User file system item info.</returns>
        public static IFileSystemItemMetadata GetUserFileSysteItemMetadata(FileSystemInfo remoteStorageItem)
        {
            IFileSystemItemMetadata userFileSystemItem;

            if (remoteStorageItem is FileInfo)
            {
                userFileSystemItem = new FileMetadataMac();
                ((FileMetadataMac)userFileSystemItem).Length = ((FileInfo)remoteStorageItem).Length;
            }
            else
            {
                userFileSystemItem = new FolderMetadataMac();
            }

            userFileSystemItem.Name = remoteStorageItem.Name;
            userFileSystemItem.Attributes = remoteStorageItem.Attributes;
            userFileSystemItem.CreationTime = remoteStorageItem.CreationTime;
            userFileSystemItem.LastWriteTime = remoteStorageItem.LastWriteTime;
            userFileSystemItem.LastAccessTime = remoteStorageItem.LastAccessTime;
            userFileSystemItem.ChangeTime = remoteStorageItem.LastWriteTime;
            userFileSystemItem.RemoteStorageItemId = Mapping.EncodePath(remoteStorageItem.FullName);
            userFileSystemItem.RemoteStorageParentItemId = Mapping.EncodePath(Directory.GetParent(remoteStorageItem.FullName).FullName);

            return userFileSystemItem;
        }
    }
}
