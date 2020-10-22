using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace VirtualFileSystem
{
    /// <summary>
    /// Maps user file system path to remote storage path and back. 
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

            string path = $"{Path.TrimEndingDirectorySeparator(Program.Settings.RemoteStorageRootPath)}{relativePath}";
            return path;
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

            string path = $"{Path.TrimEndingDirectorySeparator(Program.Settings.UserFileSystemRootPath)}{relativePath}";
            return path;
        }

        /// <summary>
        /// Gets user file system item info from the remote storage data.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <returns>User file system item info.</returns>
        public static FileSystemItemBasicInfo GetUserFileSysteItemBasicInfo(FileSystemInfo remoteStorageItem)
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

            // You will send the ETag to 
            // the server inside If-Match header togater with updated content from client.
            // This will make sure the changes on the server is not overwritten.
            //
            // In this sample, for the sake of simplicity, we use file last write time instead of ETag.
            userFileSystemItem.ETag = remoteStorageItem.LastWriteTime.ToBinary().ToString();

            // If the file is moved/renamed and the app is not running this will help us 
            // to sync the file/folder to remote storage after app starts.
            userFileSystemItem.CustomData = new CustomData
            {
                OriginalPath = Mapping.ReverseMapPath(remoteStorageItem.FullName)
            }.Serialize();

            if (remoteStorageItem is FileInfo)
            {
                ((FileBasicInfo)userFileSystemItem).Length = ((FileInfo)remoteStorageItem).Length;
            };

            return userFileSystemItem;
        }
    }
}
