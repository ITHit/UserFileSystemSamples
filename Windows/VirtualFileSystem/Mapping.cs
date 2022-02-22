using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;

namespace VirtualFileSystem
{
    /// <summary>
    /// Maps a the remote storage path and data to the user file system path and data. 
    /// </summary>
    /// <remarks>
    /// You will change methods of this class to map to your own remote storage.
    /// </remarks>
    public static class Mapping
    {
        /// <summary>
        /// Returns a user file system path that corresponds to the remote storage URI.
        /// </summary>
        /// <param name="remoteStorageUri">Remote storage URI.</param>
        /// <returns>Path in the user file system that corresponds to the <paramref name="remoteStorageUri"/>.</returns>
        public static string ReverseMapPath(string remoteStorageUri)
        {
            // Get path relative to the virtual root.
            string relativePath = Path.TrimEndingDirectorySeparator(remoteStorageUri).Substring(
                Path.TrimEndingDirectorySeparator(Program.Settings.RemoteStorageRootPath).Length);

            string path = $"{Path.TrimEndingDirectorySeparator(Program.Settings.UserFileSystemRootPath)}{relativePath}";
            return path;
        }

        /// <summary>
        /// Gets remote storage path by remote storage item ID.
        /// </summary>
        /// <remarks>
        /// As soon as System.IO .NET classes require path as an input parameter, 
        /// this function maps remote storage ID to the remote storge path.
        /// In your real-life file system you will typically request your remote storage 
        /// items by ID instead of using this method.
        /// </remarks>
        /// <returns>Path in the remote storage.</returns>
        public static string GetRemoteStoragePathById(byte[] remoteStorageId)
        {
            return WindowsFileSystemItem.GetPathByItemId(remoteStorageId);
        }

        /// <summary>
        /// Gets a user file system file/folder metadata from the remote storage file/folder data.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <remarks>
        /// In your real-life file system you will change the input parameter type of this method and rewrite it
        /// to map your remote storage item data to the user file system data.
        /// </remarks>
        /// <returns>File or folder metadata that corresponds to the <paramref name="remoteStorageItem"/>.</returns>
        public static IFileSystemItemMetadata GetUserFileSysteItemMetadata(FileSystemInfo remoteStorageItem)
        {
            IFileSystemItemMetadata userFileSystemItem;

            if (remoteStorageItem is FileInfo)
            {
                userFileSystemItem = new FileMetadata();
                ((FileMetadata)userFileSystemItem).Length = ((FileInfo)remoteStorageItem).Length;
            }
            else
            {
                userFileSystemItem = new FolderMetadata();
            }

            // Store you remote storage item ID in this property.
            // It will be passed to IEngine.GetFileSystemItemAsync() during every operation.
            userFileSystemItem.RemoteStorageItemId = WindowsFileSystemItem.GetItemIdByPath(remoteStorageItem.FullName);

            userFileSystemItem.Name = remoteStorageItem.Name;
            userFileSystemItem.Attributes = remoteStorageItem.Attributes;
            userFileSystemItem.CreationTime = remoteStorageItem.CreationTime;
            userFileSystemItem.LastWriteTime = remoteStorageItem.LastWriteTime;
            userFileSystemItem.LastAccessTime = remoteStorageItem.LastAccessTime;
            userFileSystemItem.ChangeTime = remoteStorageItem.LastWriteTime;

            return userFileSystemItem;
        }
    }
}
