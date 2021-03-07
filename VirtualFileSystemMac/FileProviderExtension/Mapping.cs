using System.IO;
using ITHit.FileSystem;
using VirtualFilesystemCommon;

namespace FileProviderExtension
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
            string relativePath = userFileSystemPath.TrimEnd(Path.DirectorySeparatorChar).Substring(
                AppGroupSettings.GetUserRootPath().TrimEnd(Path.DirectorySeparatorChar).Length);

            string path = $"{AppGroupSettings.GetRemoteRootPath().TrimEnd(Path.DirectorySeparatorChar)}{relativePath}";
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
            string relativePath = remoteStorageUri.TrimEnd(Path.DirectorySeparatorChar).Substring(
                AppGroupSettings.GetRemoteRootPath().TrimEnd(Path.DirectorySeparatorChar).Length);

            string path = $"{AppGroupSettings.GetUserRootPath().TrimEnd(Path.DirectorySeparatorChar)}{relativePath}";
            return path;
        }

        /// <summary>
        /// Gets a user file system item info from the remote storage data.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <returns>User file system item info.</returns>
        public static IFileSystemItemBasicInfo GetUserFileSysteItemBasicInfo(FileSystemInfo remoteStorageItem)
        {
            VfsFileSystemItem userFileSystemItem;

            if (remoteStorageItem is FileInfo)
            {
                FileInfo remoteStorageFile = (FileInfo)remoteStorageItem;
                userFileSystemItem = new VfsFile(remoteStorageFile.FullName, remoteStorageFile.Attributes, remoteStorageFile.CreationTime,
                                                 remoteStorageFile.LastWriteTime, remoteStorageFile.LastAccessTime, remoteStorageFile.Length);
            }
            else
            {
                userFileSystemItem = new VfsFolder(remoteStorageItem.FullName, remoteStorageItem.Attributes, remoteStorageItem.CreationTime,
                                                   remoteStorageItem.LastWriteTime, remoteStorageItem.LastAccessTime);
            }

            userFileSystemItem.Name = remoteStorageItem.FullName;
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
            userFileSystemItem.ETag = remoteStorageItem.LastWriteTime.ToBinary().ToString();

            // If the item is locked by another user, set the LockedByAnotherUser to true.
            // Here we just use the read-only attribute from remote storage item for demo purposes.
            userFileSystemItem.LockedByAnotherUser = (remoteStorageItem.Attributes & FileAttributes.ReadOnly) != 0;

            if (remoteStorageItem is FileInfo)
            {
                ((VfsFile)userFileSystemItem).Length = ((FileInfo)remoteStorageItem).Length;
            };

            return userFileSystemItem;
        }
    }
}
