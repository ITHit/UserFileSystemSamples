using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Provides method for reading and writing ETags.
    /// </summary>
    public class ETagManager
    {
        private readonly string userFileSystemPath;
        private readonly string userFileSystemRootPath;
        private readonly string serverDataFolderPath;
        private readonly ILogger logger;
        internal readonly string ETagFilePath;

        private const string eTagExt = ".etag";

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemRootPath">User file system root path.</param>
        /// <param name="serverDataFolderPath">Folder where ETags are stored.</param>
        /// <param name="logger">Logger.</param>
        internal ETagManager(string userFileSystemPath, string serverDataFolderPath, string userFileSystemRootPath, ILogger logger)
        {
            this.userFileSystemPath = userFileSystemPath;
            this.userFileSystemRootPath = userFileSystemRootPath;
            this.serverDataFolderPath = serverDataFolderPath;
            this.logger = logger;
            this.ETagFilePath = $"{GetEtagFilePath(userFileSystemPath)}{eTagExt}";
        }

        /// <summary>
        /// Creates or updates ETag associated with the file.
        /// </summary>
        /// <param name="eTag">ETag.</param>
        /// <returns></returns>
        public async Task SetETagAsync(string eTag)
        {
            // Delete ETag file if null or empty string value is passed.
            if (string.IsNullOrEmpty(eTag) && File.Exists(ETagFilePath))
            {
                DeleteETag();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ETagFilePath));
            await File.WriteAllTextAsync(ETagFilePath, eTag);
        }

        /// <summary>
        /// Gets ETag associated with a file.
        /// </summary>
        /// <returns>ETag.</returns>
        public async Task<string> GetETagAsync()
        {
            if (!File.Exists(ETagFilePath))
            {
                return null;
            }
            return await File.ReadAllTextAsync(ETagFilePath);
        }

        /// <summary>
        /// Moves ETag to a new location.
        /// </summary>
        /// <param name="userFileSystemNewPath">Path of the file in the user file system to move this Etag to.</param>
        internal async Task MoveToAsync(string userFileSystemNewPath)
        {
            // Move ETag file.
            string eTagTargetPath = GetEtagFilePath(userFileSystemNewPath);
            string eTagFileTargetPath = $"{eTagTargetPath}{eTagExt}";

            // Ensure the target directory exisit, in case we are moving into empty folder or which is offline.
            new FileInfo(eTagFileTargetPath).Directory.Create();
            File.Move(ETagFilePath, eTagFileTargetPath);

            // If this is a folder, move all eTags in this folder.
            string eTagSourceFolderPath = GetEtagFilePath(userFileSystemPath);
            if (Directory.Exists(eTagSourceFolderPath))
            {
                Directory.Move(eTagSourceFolderPath, eTagTargetPath);
            }
        }

        /// <summary>
        /// Deletes ETag associated with a file.
        /// </summary>
        internal void DeleteETag()
        {
            File.Delete(ETagFilePath);

            // If this is a folder, delete all eTags in this folder.
            string eTagFolderPath = GetEtagFilePath(userFileSystemPath);
            if (Directory.Exists(eTagFolderPath))
            {
                Directory.Delete(eTagFolderPath, true);
            }
        }

        /// <summary>
        /// Returns true if the remote storage ETag and user file system ETags are equal. False - otherwise.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <remarks>
        /// ETag is updated on the server during every document update and is sent to client with a file. 
        /// During user file system to remote storage update it is sent back to the remote storage together with a modified content. 
        /// This ensures the changes in the remote storage are not overwritten if the document on the server is modified.
        /// </remarks>
        public async Task<bool> ETagEqualsAsync(FileSystemItemMetadata remoteStorageItem)
        {
            string remoteStorageETag = remoteStorageItem.ETag;
            string userFileSystemETag = await GetETagAsync();

            if (string.IsNullOrEmpty(remoteStorageETag) && string.IsNullOrEmpty(userFileSystemETag))
            {
                // We assume the remote storage is not using ETags or no ETag is ssociated with this file/folder.
                return true;
            }

            return remoteStorageETag == userFileSystemETag;
        }

        /// <summary>
        /// Gets ETag file path (without extension).
        /// </summary>
        /// <param name="userFileSystemPath">Path of the file in user file system to get ETag path for.</param>
        private string GetEtagFilePath(string userFileSystemPath)
        {
            // Get path relative to the virtual root.
            string relativePath = userFileSystemPath.TrimEnd(Path.DirectorySeparatorChar).Substring(
                userFileSystemRootPath.TrimEnd(Path.DirectorySeparatorChar).Length);

            return $"{serverDataFolderPath.TrimEnd(Path.DirectorySeparatorChar)}{relativePath}";
        }
    }
}
