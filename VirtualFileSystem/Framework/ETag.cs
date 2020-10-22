using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace VirtualFileSystem
{
    /// <summary>
    /// Provides method for reading and writing ETags.
    /// </summary>
    internal static class ETag
    {
        /// <summary>
        /// Creates or updates ETag associated with the file.
        /// </summary>
        /// <param name="userFileSystemPath">Path in the user file system.</param>
        /// <param name="eTag">ETag.</param>
        /// <returns></returns>
        public static async Task SetETagAsync(string userFileSystemPath, string eTag)
        {
            string eTagFilePath = GetETagFilePath(userFileSystemPath);
            Directory.CreateDirectory(Path.GetDirectoryName(eTagFilePath));
            await File.WriteAllTextAsync(eTagFilePath, eTag);
        }

        /// <summary>
        /// Gets ETag associated with a file.
        /// </summary>
        /// <param name="userFileSystemPath">Path in the user file system.</param>
        /// <returns>ETag.</returns>
        public static async Task<string> GetETagAsync(string userFileSystemPath)
        {
            string eTagFilePath = GetETagFilePath(userFileSystemPath);
            if (!File.Exists(eTagFilePath))
            {
                return null;
            }
            return await File.ReadAllTextAsync(eTagFilePath);
        }

        /// <summary>
        /// Deletes ETag associated with a file.
        /// </summary>
        /// <param name="userFileSystemSourcePath">Path in the user file system.</param>
        public static void DeleteETag(string userFileSystemSourcePath)
        {
            File.Delete(GetETagFilePath(userFileSystemSourcePath));
        }

        /// <summary>
        /// Gets path to the file in which ETag is stored based on the provided user file system path.
        /// </summary>
        /// <param name="userFileSystemPath">Path to the file or folder to get the ETag file path.</param>
        /// <returns>Path to the file that contains ETag.</returns>
        public static string GetETagFilePath(string userFileSystemPath)
        {
            // Get path relative to the virtual root.
            string relativePath = Path.TrimEndingDirectorySeparator(userFileSystemPath).Substring(
                Path.TrimEndingDirectorySeparator(Program.Settings.UserFileSystemRootPath).Length);

            string path = $"{Path.TrimEndingDirectorySeparator(Program.Settings.ServerDataFolderPath)}{relativePath}.etag";
            return path;
        }

        /// <summary>
        /// Returns true if the remote storage ETag and user file system ETags are equal. False - otherwise.
        /// </summary>
        /// <param name="userFileSystemPath">User file system item.</param>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <remarks>
        /// ETag is updated on the server during every document update and is sent to client with a file. 
        /// During client->server update it is sent back to the remote storage together with a modified content. 
        /// This ensures the changes on the server are not overwritten if the document on the server is modified.
        /// </remarks>
        internal static async Task<bool> ETagEqualsAsync(string userFileSystemPath, FileSystemItemBasicInfo remoteStorageItem)
        {
            string remoteStorageETag = remoteStorageItem.ETag;

            // Intstead of the real ETag we store remote storage LastWriteTime when 
            // creating and updating files/folders.
            string userFileSystemETag = await ETag.GetETagAsync(userFileSystemPath);
            if (string.IsNullOrEmpty(userFileSystemETag))
            {
                // No ETag associated with the file. This is a new file created in user file system.
                return false;
            }

            return remoteStorageETag == userFileSystemETag;
        }
    }
}
