using ITHit.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VirtualFileSystem
{
    /// <summary>
    /// Maps user file system path to remote storage path and back. 
    /// Creates user file system file or folder item based on remote storate item info.
    /// </summary>
    internal static class Mapping
    {
        /// <summary>
        /// Returns remote storage path that corresponds to user file system path.
        /// </summary>
        /// <param name="userFileSystemPath">Full path in user file system.</param>
        /// <returns>Path in remote storage that corresponds to <paramref name="userFileSystemPath"/>.</returns>
        public static string MapPath(string userFileSystemPath)
        {
            // Get path relative to the virtual root.
            string relativePath = Path.TrimEndingDirectorySeparator(userFileSystemPath).Substring(
                Path.TrimEndingDirectorySeparator(Program.Settings.UserFileSystemRootPath).Length);

            // Get this folder path under the source folder.
            string sourcePath = $"{Path.TrimEndingDirectorySeparator(Program.Settings.RemoteStorageRootPath)}{relativePath}";
            return sourcePath;
        }

        /// <summary>
        /// Returns user file system path that corresponds to remote storage path.
        /// </summary>
        /// <param name="remoteStoragePath">Full path in remote storage.</param>
        /// <returns>Path in user file system that corresponds to <paramref name="remoteStoragePath"/>.</returns>
        public static string ReverseMapPath(string remoteStoragePath)
        {
            // Get path relative to the virtual root.
            string relativePath = Path.TrimEndingDirectorySeparator(remoteStoragePath).Substring(
                Path.TrimEndingDirectorySeparator(Program.Settings.RemoteStorageRootPath).Length);

            // Get this folder path under the source folder.
            string sourcePath = $"{Path.TrimEndingDirectorySeparator(Program.Settings.UserFileSystemRootPath)}{relativePath}";
            return sourcePath;
        }

        /// <summary>
        /// Gets user file system item info from the remote storage data.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <returns>User file system item info.</returns>
        public static FileSystemItemBasicInfo GetUserFileSysteItemInfo(FileSystemInfo remoteStorageItem)
        {
            FileSystemItemBasicInfo userFileSystemItem;

            if (remoteStorageItem is FileInfo)
            {
                userFileSystemItem = new FileBasicInfo();
            }
            else
            {
                userFileSystemItem = new FolderBasicInfo();
            }

            userFileSystemItem.Name = remoteStorageItem.Name;
            userFileSystemItem.Attributes = remoteStorageItem.Attributes;
            userFileSystemItem.CreationTime = remoteStorageItem.CreationTime;
            userFileSystemItem.LastWriteTime = remoteStorageItem.LastWriteTime;
            userFileSystemItem.LastAccessTime = remoteStorageItem.LastAccessTime;
            userFileSystemItem.ChangeTime = remoteStorageItem.LastWriteTime;

            // Here you will typically store the file ETag. You will send the ETag to 
            // the server inside If-Match header togater with updated content from client.
            // This will make sure the file on the server is not modified.
            //
            // In this sample, for the sake of simplicity, we use file last write time.
            userFileSystemItem.CustomData = BitConverter.GetBytes(remoteStorageItem.LastWriteTime.ToBinary());

            if (remoteStorageItem is FileInfo)
            {
                ((FileBasicInfo)userFileSystemItem).Length = ((FileInfo)remoteStorageItem).Length;
            };

            return userFileSystemItem;
        }
    }
}
