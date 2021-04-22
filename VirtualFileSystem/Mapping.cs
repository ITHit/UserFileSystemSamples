using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ITHit.FileSystem.Samples.Common;

namespace VirtualFileSystem
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

            string path = $"{Path.TrimEndingDirectorySeparator(Program.Settings.RemoteStorageRootPath)}{relativePath}";
            return path;
        }

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
        public static FileSystemItemMetadata GetUserFileSysteItemMetadata(FileSystemInfo remoteStorageItem)
        {
            FileSystemItemMetadata userFileSystemItem;

            if (remoteStorageItem is FileInfo)
            {
                userFileSystemItem = new FileMetadata();
            }
            else
            {
                userFileSystemItem = new FolderMetadata();
            }

            userFileSystemItem.Name = remoteStorageItem.Name;
            userFileSystemItem.Attributes = remoteStorageItem.Attributes;
            userFileSystemItem.CreationTime = remoteStorageItem.CreationTime;
            userFileSystemItem.LastWriteTime = remoteStorageItem.LastWriteTime;
            userFileSystemItem.LastAccessTime = remoteStorageItem.LastAccessTime;
            userFileSystemItem.ChangeTime = remoteStorageItem.LastWriteTime;

            // You will send the ETag to 
            // the server inside If-Match header togeter with updated content from client.
            // This will make sure the changes on the server is not overwritten.
            //
            // In this sample, for the sake of simplicity, we use file last write time instead of ETag.
            userFileSystemItem.ETag = userFileSystemItem.LastWriteTime.ToUniversalTime().ToString("o");

            // If the item is locked by another user, set the LockedByAnotherUser to true.
            // Here we just use the read-only attribute from remote storage item for demo purposes.
            userFileSystemItem.LockedByAnotherUser = (remoteStorageItem.Attributes & System.IO.FileAttributes.ReadOnly) != 0;

            if (remoteStorageItem is FileInfo)
            {
                ((FileMetadata)userFileSystemItem).Length = ((FileInfo)remoteStorageItem).Length;
            };

            // Set custom columns to be displayed in file manager.
            // We create property definitions when registering the sync root with corresponding IDs.
            List<FileSystemItemPropertyData> customProps = new List<FileSystemItemPropertyData>();
            if (userFileSystemItem.LockedByAnotherUser)
            {
                customProps.AddRange(
                    new ServerLockInfo()
                    {
                        LockToken = "token",
                        Owner = "User Name",
                        Exclusive = true,
                        LockExpirationDateUtc = DateTimeOffset.Now.AddMinutes(30)
                    }.GetLockProperties(Path.Combine(Program.Settings.IconsFolderPath, "LockedByAnotherUser.ico"))
                );
            }
            userFileSystemItem.CustomProperties = customProps;

            return userFileSystemItem;
        }
    }
}
