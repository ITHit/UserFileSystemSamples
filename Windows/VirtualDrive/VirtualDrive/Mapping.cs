using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;

namespace VirtualDrive
{
    /// <summary>
    /// Maps a the remote storage path and data to the user file system path and data. 
    /// </summary>
    /// <remarks>
    /// You will change methods of this class to map to your own remote storage.
    /// </remarks>
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
            this.userFileSystemRootPath = userFileSystemRootPath;
            this.remoteStorageRootPath = remoteStorageRootPath;
        }

        /// <summary>
        /// Returns a remote storage URI that corresponds to the user file system path.
        /// </summary>
        /// <param name="userFileSystemPath">Full path in the user file system.</param>
        /// <returns>Remote storage URI that corresponds to the <paramref name="userFileSystemPath"/>.</returns>
        public string MapPath(string userFileSystemPath)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns a user file system path that corresponds to the remote storage URI.
        /// </summary>
        /// <param name="remoteStorageUri">Remote storage URI.</param>
        /// <returns>Path in the user file system that corresponds to the <paramref name="remoteStorageUri"/>.</returns>
        public string ReverseMapPath(string remoteStorageUri)
        {
            // Get path relative to the virtual root.
            string relativePath = Path.TrimEndingDirectorySeparator(remoteStorageUri).Substring(
                Path.TrimEndingDirectorySeparator(remoteStorageRootPath).Length);

            string path = $"{Path.TrimEndingDirectorySeparator(userFileSystemRootPath)}{relativePath}";
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
        /// Tries to get remote storage path by remote storage item ID.
        /// </summary>
        /// <remarks>
        /// The item may be already deleted or moved at the time of request, 
        /// so we use the try-method to reduce number of exceptions in the log and improve performance.
        /// </remarks>
        /// <param name="remoteStorageId">Remote storage ID.</param>
        /// <param name="remoteStoragePath">Remote storage path.</param>
        /// <returns>True if the method completed successfully, false - otherwise.</returns>
        public static bool TryGetRemoteStoragePathById(byte[] remoteStorageId, out string remoteStoragePath)
        {
            if (WindowsFileSystemItem.TryGetPathByItemId(remoteStorageId, out remoteStoragePath))
            {
                // Extra check to avoid errors in the log if the item was deleted while the Engine was still processing it.
                if (!IsRecycleBin(remoteStoragePath))
                {
                    return true;
                }
            }

            remoteStoragePath = null;
            return false;
        }

        /// <summary>
        /// Returns true if the path points to a recycle bin folder.
        /// </summary>
        /// <param name="path">Path to the file or folder.</param>
        private static bool IsRecycleBin(string path)
        {
            return path.IndexOf("\\$Recycle.Bin", StringComparison.InvariantCultureIgnoreCase) != -1;
        }

        /// <summary>
        /// Gets a user file system file/folder metadata from the remote storage file/folder data.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage item info.</param>
        /// <remarks>
        /// In your real-life file system you will change the input parameter type of this method and rewrite it
        /// to map your remote storage item data to the user file system data.
        /// </remarks>
        /// <returns>File or folder metadata that corresponds to the <paramref name="remoteStorageItem"/> parameter.</returns>
        public static IMetadata GetMetadata(FileSystemInfo remoteStorageItem)
        {
            IMetadata metadata;

            if (remoteStorageItem is FileInfo)
            {
                metadata = new FileMetadata();
                ((FileMetadata)metadata).Length = ((FileInfo)remoteStorageItem).Length;
            }
            else
            {
                metadata = new FolderMetadata();
            }

            // Store your remote storage item ID in this property.
            // It will be passed to the IEngine.GetFileSystemItemAsync() method during every operation.
            metadata.RemoteStorageItemId = WindowsFileSystemItem.GetItemIdByPath(remoteStorageItem.FullName);

            metadata.Name = remoteStorageItem.Name;
            metadata.Attributes = remoteStorageItem.Attributes;
            metadata.CreationTime = remoteStorageItem.CreationTime;
            metadata.LastWriteTime = remoteStorageItem.LastWriteTime;
            metadata.LastAccessTime = remoteStorageItem.LastAccessTime;
            metadata.ChangeTime = remoteStorageItem.LastWriteTime;

            // Add custom properties here to be displayed in file manager.
            // - We create property definitions when registering the sync root with corresponding IDs.
            // - The columns are rendered in IFileSystemItem.GetPropertiesAsync() call.
            // metadata.Properties.Add(...) ;

            return metadata;
        }
    }
}
