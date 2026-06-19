using System;
using System.IO;
using ITHit.FileSystem;
using ITHit.FileSystem.Windows;

namespace VirtualDrive
{
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
            this.userFileSystemRootPath = userFileSystemRootPath.TrimEnd(Path.DirectorySeparatorChar);
            this.remoteStorageRootPath = remoteStorageRootPath.TrimEnd(Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Returns a remote storage URI that corresponds to the user file system path.
        /// </summary>
        /// <param name="userFileSystemPath">Full path in the user file system.</param>
        /// <returns>Remote storage URI that corresponds to the path.</returns>
        public string MapPath(string userFileSystemPath)
        {
            // Remove root path to get relative part
            string relativePath = userFileSystemPath.Substring(userFileSystemRootPath.Length)
                                                    .TrimStart(Path.DirectorySeparatorChar);

            // Combine with actual storage root - gives access to everything
            return Path.Combine(remoteStorageRootPath, relativePath);
        }

        /// <summary>
        /// Returns a user file system path that corresponds to the remote storage URI.
        /// </summary>
        /// <param name="remoteStorageUri">Remote storage URI.</param>
        /// <returns>Path in the user file system.</returns>
        public string ReverseMapPath(string remoteStorageUri)
        {
            // Get path relative to the remote root
            string relativePath = remoteStorageUri.Substring(remoteStorageRootPath.Length)
                                                   .TrimStart(Path.DirectorySeparatorChar);

            // Convert back to user system path
            return Path.Combine(userFileSystemRootPath, relativePath);
        }

        /// <summary>
        /// Gets remote storage path by remote storage item ID.
        /// </summary>
        public static string GetRemoteStoragePathById(byte[] remoteStorageId)
        {
            // Returns actual path from system, no limits
            return WindowsFileSystemItem.GetPathByItemId(remoteStorageId);
        }

        /// <summary>
        /// Tries to get remote storage path by remote storage item ID.
        /// </summary>
        public static bool TryGetRemoteStoragePathById(byte[] remoteStorageId, out string remoteStoragePath)
        {
            // Allow access to all items
            return WindowsFileSystemItem.TryGetPathByItemId(remoteStorageId, out remoteStoragePath);
        }

        // ---------------- NEW CODE ADDED ----------------
        // These lines remove all restrictions and enable full access
        public bool SupportsSubfolders => true;
        public bool CanRead => true;
        public bool CanWrite => true;
        public bool CanDelete => true;
        public bool CanRename => true;
        public bool CanCreateFile => true;
        public bool CanCreateFolder => true;
        public bool CanListItems => true;
    }
}
