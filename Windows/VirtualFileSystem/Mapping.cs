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
    /// Maps a user file system path to the remote storage path and back. 
    /// </summary>
    /// <remarks>
    /// You will change methods of this class to map the user file system path to your remote storage path.
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
        /// Gets a user file system item info from the remote storage data.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <returns>User file system item info.</returns>
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

            // Store you item ID here. It will be passed to IEngine.GetFileSystemItemAsync() during every operation.
            // Note that the file is deleted during MS Office transactional save and iten ID will be deleted with it.
            // See Virtual Drive sample for MS Office documents editing.
            userFileSystemItem.ItemId = WindowsFileSystemItem.GetItemIdByPath(remoteStorageItem.FullName);

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
