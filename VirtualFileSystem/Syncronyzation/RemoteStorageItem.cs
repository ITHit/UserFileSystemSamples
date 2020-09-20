using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace VirtualFileSystem.Syncronyzation
{
    /// <summary>
    /// Provides methods for synching user file system to remote storage.
    /// Creates, updates and delets files and folders based on the info from user file system.
    /// </summary>
    public class RemoteStorageItem
    {
        /// <summary>
        /// Path to the file or folder in remote storage.
        /// </summary>
        private string remoteStoragePath;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="remoteStoragePath">File or folder path in remote storage.</param>
        internal RemoteStorageItem(string remoteStoragePath)
        {
            if (string.IsNullOrEmpty(remoteStoragePath))
            {
                throw new ArgumentNullException("remoteStoragePath");
            }

            this.remoteStoragePath = remoteStoragePath;
        }

        public static async Task CreateAsync(string remoteStoragePath, string userFileSystemPath)
        {
            if (FsPath.IsFile(userFileSystemPath))
            {
                await new RemoteStorageItem(remoteStoragePath).CreateOrUpdateFileAsync(userFileSystemPath, true);
            }
            else
            {
                await new RemoteStorageItem(remoteStoragePath).CreateOrUpdateFolderAsync(userFileSystemPath, true);
            }
        }

        public async Task UpdateAsync(string userFileSystemPath)
        {
            if (FsPath.IsFile(userFileSystemPath))
            {
                await CreateOrUpdateFileAsync(userFileSystemPath, false);
            }
            else
            {
                await CreateOrUpdateFolderAsync(userFileSystemPath, false);
            }
        }

        private async Task CreateOrUpdateFileAsync(string userFileSystemPath, bool create)
        {
            try
            {
                Program.RemoteStorageMonitorInstance.Enabled = false; // Disable RemoteStorageMonitor to avoid circular calls.

                FileInfo userFileSystemFile = new FileInfo(userFileSystemPath);
                FileInfo remoteStorageFile = new FileInfo(remoteStoragePath);

                FileMode fileMode = create ? FileMode.CreateNew : FileMode.Open;


                await using (FileStream userFileSystemStream = userFileSystemFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // If another thread is trying to sync the same file, this call will fail in other threads.
                    // In your implemntation you must lock your remote storage file, or block it for reading and writing by other means.
                    await using (FileStream remoteStorageStream = remoteStorageFile.Open(fileMode, FileAccess.Write, FileShare.None))
                    {
                        userFileSystemFile.Refresh(); // Ensures LastWriteTimeUtc is in sync with file content after Open() was called.

                        // Update ETag/LastWriteTime in user file system, so the synchronyzation or remote storage monitor would not start the new update.
                        byte[] customData = BitConverter.GetBytes(userFileSystemFile.LastWriteTime.ToBinary());
                        PlaceholderItem.SetCustomData(userFileSystemStream.SafeFileHandle, customData);

                        // Update remote storage file content.
                        await userFileSystemStream.CopyToAsync(remoteStorageStream);
                        remoteStorageStream.SetLength(userFileSystemStream.Length);

                        // Update remote storage file basic info.
                        WindowsFileSystemItem.SetFileInformation(remoteStorageStream.SafeFileHandle,
                                    userFileSystemFile.Attributes & (FileAttributes)~FileAttributesExt.Pinned, // Remove Pinned flag.
                                    userFileSystemFile.CreationTimeUtc,
                                    userFileSystemFile.LastWriteTimeUtc,
                                    userFileSystemFile.LastAccessTimeUtc,
                                    userFileSystemFile.LastWriteTimeUtc);

                        // If you are using ETags, here you will also send to the remote storage a new file ETag.

                        PlaceholderItem.SetInSync(userFileSystemStream.SafeFileHandle, true);
                    }
                }
            }
            finally
            {
                Program.RemoteStorageMonitorInstance.Enabled = true;
            }
        }

        private async Task CreateOrUpdateFolderAsync(string userFileSystemPath, bool create)
        {
            try
            {
                Program.RemoteStorageMonitorInstance.Enabled = false; // Disable RemoteStorageMonitor to avoid circular calls.

                DirectoryInfo userFileSystemFolder = new DirectoryInfo(userFileSystemPath);
                DirectoryInfo remoteStorageFolder = new DirectoryInfo(remoteStoragePath);

                remoteStorageFolder.Create();

                // Update ETag/LastWriteTime in user file system, so the synchronyzation or remote storage monitor would not start the new update.
                byte[] customData = BitConverter.GetBytes(userFileSystemFolder.LastWriteTime.ToBinary());
                PlaceholderItem.GetItem(userFileSystemPath).SetCustomData(customData);

                // Remove Pinned, Unpinned and Offline flags.
                remoteStorageFolder.Attributes =
                    userFileSystemFolder.Attributes
                    & (FileAttributes)~FileAttributesExt.Pinned
                    & (FileAttributes)~FileAttributesExt.Unpinned
                    & ~FileAttributes.Offline;

                remoteStorageFolder.CreationTimeUtc = userFileSystemFolder.CreationTimeUtc;
                remoteStorageFolder.LastWriteTimeUtc = userFileSystemFolder.LastWriteTimeUtc;
                remoteStorageFolder.LastAccessTimeUtc = userFileSystemFolder.LastAccessTimeUtc;
                remoteStorageFolder.LastWriteTimeUtc = userFileSystemFolder.LastWriteTimeUtc;

                PlaceholderItem.GetItem(userFileSystemPath).SetInSync(true);
            }
            finally
            {
                Program.RemoteStorageMonitorInstance.Enabled = true;
            }
        }
    }
}
