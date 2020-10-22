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
    /// Represents a file or a folder in the remote storage. Contains methods common for both files and folders.
    /// </summary>
    internal class UserFileSystemItem
    {
        /// <summary>
        /// Path of this file of folder in the user file system.
        /// </summary>
        protected string UserFileSystemPath;

        /// <summary>
        /// Path of this file or folder in the remote storage.
        /// </summary>
        protected string RemoteStoragePath;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">Path of this file of folder in the user file system.</param>
        public UserFileSystemItem(string userFileSystemPath)
        {
            this.UserFileSystemPath = userFileSystemPath;
            this.RemoteStoragePath = Mapping.MapPath(userFileSystemPath);
        }

        /// <summary>
        /// Moves file or folder to new location in the remote storage.
        /// </summary>
        /// <param name="userFileSystemNewPath">Target path of this file of folder in user file system.</param>
        public async Task MoveToAsync(string userFileSystemNewPath)
        {
            string remoteStorageOldPath = RemoteStoragePath;
            string remoteStorageNewPath = Mapping.MapPath(userFileSystemNewPath);

            FileSystemInfo remoteStorageOldItem = FsPath.GetFileSystemItem(remoteStorageOldPath);
            if (remoteStorageOldItem != null)
            {
                try
                {
                    // Disable RemoteStorageMonitor to avoid circular calls.
                    // This is only required because of the specifics of the simplicity of this example.
                    Program.RemoteStorageMonitorInstance.Enabled = false;

                    if (remoteStorageOldItem is FileInfo)
                    {
                        (remoteStorageOldItem as FileInfo).MoveTo(remoteStorageNewPath, true);
                    }
                    else
                    {
                        (remoteStorageOldItem as DirectoryInfo).MoveTo(remoteStorageNewPath);
                    }
                }
                finally
                {
                    Program.RemoteStorageMonitorInstance.Enabled = true;
                }
            }
        }

        /// <summary>
        /// Deletes this file or folder in the remote storage.
        /// </summary>
        public async Task DeleteAsync()
        {
            FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(RemoteStoragePath);
            if (remoteStorageItem!=null)
            {
                try
                {
                    // Disable RemoteStorageMonitor to avoid circular calls.
                    // This is only required because of the specifics of the simplicity of this example.
                    Program.RemoteStorageMonitorInstance.Enabled = false;

                    if (remoteStorageItem is FileInfo)
                    {
                        remoteStorageItem.Delete();
                    }
                    else
                    {
                        (remoteStorageItem as DirectoryInfo).Delete(true);
                    }
                }
                finally
                {
                    Program.RemoteStorageMonitorInstance.Enabled = true;
                }
            }
        }

        /// <summary>
        /// Creates or updates file in the remote storage.
        /// </summary>
        /// <param name="remoteStoragePath">Path of the file to be created or updated in the remote storage.</param>
        /// <param name="newInfo">New information about the file, such as modification date, attributes, custom data, etc.</param>
        /// <param name="mode">Specifies if a new file should be created or existing file should be updated.</param>
        /// <param name="newContentStream">New file content or null if the file content is not modified.</param>
        /// <returns>New ETag returned from the remote storage.</returns>
        protected static async Task<string> CreateOrUpdateFileAsync(string remoteStoragePath, IFileBasicInfo newInfo, FileMode mode, Stream newContentStream = null)
        {
            FileInfo remoteStorageItem = new FileInfo(remoteStoragePath);
            try
            {

                Program.RemoteStorageMonitorInstance.Enabled = false; // Disable RemoteStorageMonitor to avoid circular calls.

                // If another thread is trying to sync the same file, this call will fail in other threads.
                // In your implementation you must lock your remote storage file, or block it for reading and writing by other means.
                await using (FileStream remoteStorageStream = remoteStorageItem.Open(mode, FileAccess.Write, FileShare.None))
                {
                    string userFileSystemPath = Mapping.ReverseMapPath(remoteStoragePath);
                    if (mode == FileMode.Open)
                    {
                        // Verify that the item in the remote storage is not modified since it was downloaded to the user file system.
                        // In your real-life application you will send the ETag to the server as part of the update request.
                        FileSystemItemBasicInfo itemInfo = Mapping.GetUserFileSysteItemBasicInfo(remoteStorageItem);
                        if (!(await ETag.ETagEqualsAsync(userFileSystemPath, itemInfo)))
                        {
                            throw new ConflictException(Modified.Server, "Item is modified in the remote storage, ETags not equal.");
                        }
                    }

                    // Update ETag/LastWriteTime in user file system, so the synchronyzation or remote storage monitor would not start the new update.
                    // This is only required to avoid circular updates because of the simplicity of this sample.
                    // In your real-life application you will receive a new ETag from server in the update response.
                    string eTag = newInfo.LastWriteTime.ToBinary().ToString();
                    await ETag.SetETagAsync(userFileSystemPath, eTag);

                    // Update remote storage file content.
                    if (newContentStream != null)
                    {
                        await newContentStream.CopyToAsync(remoteStorageStream);
                        remoteStorageStream.SetLength(newContentStream.Length);
                    }

                    // Update remote storage file basic info.
                    WindowsFileSystemItem.SetFileInformation(
                        remoteStorageStream.SafeFileHandle, 
                        newInfo.Attributes,
                        newInfo.CreationTime,
                        newInfo.LastWriteTime,
                        newInfo.LastAccessTime,
                        newInfo.LastWriteTime);

                    return eTag;
                }
            }
            finally
            {
                Program.RemoteStorageMonitorInstance.Enabled = true;
            }
        }


        /// <summary>
        /// Creates or updates folder in the remote storage.
        /// </summary>
        /// <param name="remoteStoragePath">Path of the folder to be created or updated in the remote storage.</param>
        /// <param name="newInfo">New information about the folder, such as modification date, attributes, custom data, etc.</param>
        /// <param name="mode">Specifies if a new folder should be created or existing folder should be updated.</param>
        /// <returns>New ETag returned from the remote storage.</returns>
        protected async Task<string> CreateOrUpdateFolderAsync(string remoteStoragePath, IFolderBasicInfo newInfo, FileMode mode)
        {
            DirectoryInfo remoteStorageItem = new DirectoryInfo(remoteStoragePath);
            try
            {
                Program.RemoteStorageMonitorInstance.Enabled = false; // Disable RemoteStorageMonitor to avoid circular calls.

                remoteStorageItem.Create();

                string userFileSystemPath = Mapping.ReverseMapPath(remoteStoragePath);
                if (mode == FileMode.Open)
                {
                    // Verify that the item in the remote storage is not modified since it was downloaded to the user file system.
                    // In your real-life application you will send the ETag to the server as part of the update request.
                    FileSystemItemBasicInfo itemInfo = Mapping.GetUserFileSysteItemBasicInfo(remoteStorageItem);
                    if (!(await ETag.ETagEqualsAsync(userFileSystemPath, itemInfo)))
                    {
                        throw new ConflictException(Modified.Server, "Item is modified in the remote storage, ETags not equal.");
                    }
                }

                // Update ETag/LastWriteTime in user file system, so the synchronyzation or remote storage monitor would not start the new update.
                // This is only required to avoid circular updates because of the simplicity of this sample.
                // In your real-life application you will receive a new ETag from server in the update response.
                string eTag = newInfo.LastWriteTime.ToBinary().ToString();
                await ETag.SetETagAsync(userFileSystemPath, eTag);
                /*
                using (WindowsFileSystemItem userFileSystemWinItem = WindowsFileSystemItem.OpenWriteAttributes(userFileSystemPath, FileMode.Open, FileShare.ReadWrite))
                {
                    PlaceholderItemExtensions.SetCustomData(
                        userFileSystemWinItem.SafeHandle, newInfo.LastWriteTime.ToBinary().ToString(), userFileSystemPath);
                }
                */

                remoteStorageItem.Attributes = newInfo.Attributes;
                remoteStorageItem.CreationTimeUtc = newInfo.CreationTime;
                remoteStorageItem.LastWriteTimeUtc = newInfo.LastWriteTime;
                remoteStorageItem.LastAccessTimeUtc = newInfo.LastAccessTime;
                remoteStorageItem.LastWriteTimeUtc = newInfo.LastWriteTime;

                return eTag;
            }
            finally
            {
                Program.RemoteStorageMonitorInstance.Enabled = true;
            }
        }

        /*
        /// <summary>
        /// Creates or updates folder in the remote storage.
        /// </summary>
        /// <param name="newInfo">New information about the folder, such as modification date, attributes, custom data, etc.</param>
        /// <param name="mode">Specifies if a new folder should be created or existing folder should be updated.</param>
        protected static async Task CreateOrUpdateFolderAsync(string remoteStoragePath, FolderBasicInfo newInfo, FileMode mode)
        {
            try
            {
                DirectoryInfo remoteStorageItem = new DirectoryInfo(remoteStoragePath);

                Program.RemoteStorageMonitorInstance.Enabled = false; // Disable RemoteStorageMonitor to avoid circular calls.

                FileMode remoteStorageItemOpenMode = mode;
                if (newInfo is FolderBasicInfo)
                {
                    Directory.CreateDirectory(remoteStoragePath);
                    remoteStorageItemOpenMode = FileMode.Open;
                }

                // If another thread is trying to sync the same file, this call will fail in other threads.
                // In your implementation you must lock your remote storage file, or block it for reading and writing by other means.
                using (WindowsFileSystemItem remoteStorageWinItem = WindowsFileSystemItem.OpenGenericWrite(remoteStoragePath, remoteStorageItemOpenMode, FileShare.None))
                //await using (FileStream remoteStorageStream = remoteStorageFile.Open(mode, FileAccess.Write, FileShare.None))
                {
                    string userFileSystemPath = Mapping.ReverseMapPath(remoteStoragePath);
                    if (mode == FileMode.Open)
                    {
                        // Verify that the file in remote storage is not modified since it was downloaded to user file system.
                        // In your real-life application you will send the ETag to server as part of the update request.
                        if (!(await Mapping.ETagEqualsAsync(userFileSystemPath, remoteStorageItem)))
                        {
                            throw new ConflictException(Modified.Server, "Item is modified in the remote storage, ETags not equal.");
                        }
                    }

                    // Update ETag/LastWriteTime in user file system, so the synchronyzation or remote storage monitor would not start the new update.
                    // This is only required to avoid circular updates because of the simplicity of this sample.
                    // In your real-life application you will receive a new ETag from server in the update response.
                    using (WindowsFileSystemItem userFileSystemWinItem = WindowsFileSystemItem.OpenWriteAttributes(userFileSystemPath, FileMode.Open, FileShare.ReadWrite))
                    {
                        PlaceholderItemExtensions.SetCustomData(
                            userFileSystemWinItem.SafeHandle, newInfo.LastWriteTime.ToBinary().ToString(), userFileSystemPath);
                    }



                    // Update remote storage file basic info.
                    WindowsFileSystemItem.SetFileInformation(
                        remoteStorageWinItem.SafeHandle,
                        newInfo.Attributes,
                        newInfo.CreationTime,
                        newInfo.LastWriteTime,
                        newInfo.LastAccessTime,
                        newInfo.LastWriteTime);

                    // Here you will set a new ETag returned by the remote storage.
                }
            }
            finally
            {
                Program.RemoteStorageMonitorInstance.Enabled = true;
            }
        }
        */
    }
}
