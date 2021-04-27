using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;


namespace VirtualFileSystem
{
    /// <summary>
    /// Represents a file or a folder in the remote storage. Contains methods common for both files and folders.
    /// </summary>
    /// <remarks>You will change methods of this class to read/write data from/to your remote storage.</remarks>
    internal class VirtualFileSystemItem : IVirtualFileSystemItem, IVirtualLock
    {
        /// <summary>
        /// Path of this file of folder in the user file system.
        /// </summary>
        protected readonly string UserFileSystemPath;

        /// <summary>
        /// Path of this file or folder in the remote storage.
        /// </summary>
        protected readonly string RemoteStoragePath;

        /// <summary>
        /// Virtual Drive instance that created this item.
        /// </summary>
        protected readonly VirtualDrive VirtualDrive;

        /// <summary>
        /// Logger.
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">Path of this file of folder in the user file system.</param>
        /// <param name="virtualDrive">Virtual Drive instance that created this item.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFileSystemItem(string userFileSystemPath, VirtualDrive virtualDrive, ILogger logger)
        {
            this.UserFileSystemPath = userFileSystemPath;
            this.VirtualDrive = virtualDrive;
            this.RemoteStoragePath = Mapping.MapPath(userFileSystemPath);
            this.Logger = logger;
        }

        /// <summary>
        /// Renames or moves file or folder to a new location in the remote storage.
        /// </summary>
        /// <param name="userFileSystemNewPath">Target path of this file or folder in the user file system.</param>
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
                    Program.VirtualDrive.RemoteStorageMonitor.Enabled = false;

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
                    Program.VirtualDrive.RemoteStorageMonitor.Enabled = true;
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
                    Program.VirtualDrive.RemoteStorageMonitor.Enabled = false;

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
                    Program.VirtualDrive.RemoteStorageMonitor.Enabled = true;
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
        /// <param name="eTagOld">The ETag to be sent to the remote storage as part of the update request to make sure the content is not overwritten.</param>
        /// <param name="lockInfo">Information about the lock. Null if the item is not locked.</param>
        /// <returns>The new ETag returned from the remote storage.</returns>
        protected async Task<string> CreateOrUpdateFileAsync(
            string remoteStoragePath, IFileMetadata newInfo, FileMode mode, Stream newContentStream = null, string eTagOld = null, ServerLockInfo lockInfo = null)
        {
            FileInfo remoteStorageItem = new FileInfo(remoteStoragePath);

            try
            {
                Program.VirtualDrive.RemoteStorageMonitor.Enabled = false; // Disable RemoteStorageMonitor to avoid circular calls.

                // If another thread is trying to sync the same file, this call will fail in other threads.
                // In your implementation you must lock your remote storage file, or block it for reading and writing by other means.
                await using (FileStream remoteStorageStream = remoteStorageItem.Open(mode, FileAccess.Write, FileShare.None))
                {
                    string userFileSystemPath = Mapping.ReverseMapPath(remoteStoragePath);
                    if (mode == FileMode.Open)
                    {
                        // Verify that the item in the remote storage is not modified since it was downloaded to the user file system.
                        // In your real-life application you will send the ETag to the server as part of the update request.
                        FileSystemItemMetadataExt itemInfo = Mapping.GetUserFileSysteItemMetadata(remoteStorageItem);
                        if (!(await VirtualDrive.GetETagManager(userFileSystemPath).ETagEqualsAsync(itemInfo)))
                        {
                            throw new ConflictException(Modified.Server, "Item is modified in the remote storage, ETags not equal.");
                        }
                    }

                    // Update ETag/LastWriteTime in user file system, so the synchronyzation or remote storage monitor would not start the new update.
                    // This is only required to avoid circular updates because of the simplicity of this sample.
                    // In your real-life application you will receive a new ETag from the server in the update response
                    // and return it from this method.
                    string eTagNew = newInfo.LastWriteTime.ToUniversalTime().ToString("o");
                    await VirtualDrive.GetETagManager(userFileSystemPath).SetETagAsync(eTagNew);

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

                    return eTagNew;
                }
            }
            finally
            {
                Program.VirtualDrive.RemoteStorageMonitor.Enabled = true;
            }
        }


        /// <summary>
        /// Creates or updates folder in the remote storage.
        /// </summary>
        /// <param name="remoteStoragePath">Path of the folder to be created or updated in the remote storage.</param>
        /// <param name="newInfo">New information about the folder, such as modification date, attributes, custom data, etc.</param>
        /// <param name="mode">Specifies if a new folder should be created or existing folder should be updated.</param>
        /// <param name="eTagOld">The ETag to be sent to the remote storage as part of the update request to make sure the content is not overwritten.</param>
        /// <param name="lockInfo">Information about the lock. Null if the item is not locked.</param>
        /// <returns>The new ETag returned from the remote storage.</returns>
        protected async Task<string> CreateOrUpdateFolderAsync(
            string remoteStoragePath, IFolderMetadata newInfo, FileMode mode, string eTagOld = null, ServerLockInfo lockInfo = null)
        {
            DirectoryInfo remoteStorageItem = new DirectoryInfo(remoteStoragePath);

            try
            {
                Program.VirtualDrive.RemoteStorageMonitor.Enabled = false; // Disable RemoteStorageMonitor to avoid circular calls.

                remoteStorageItem.Create();

                string userFileSystemPath = Mapping.ReverseMapPath(remoteStoragePath);
                if (mode == FileMode.Open)
                {
                    // Verify that the item in the remote storage is not modified since it was downloaded to the user file system.
                    // In your real-life application you will send the ETag to the server as part of the update request.
                    FileSystemItemMetadataExt itemInfo = Mapping.GetUserFileSysteItemMetadata(remoteStorageItem);
                    if (!(await VirtualDrive.GetETagManager(userFileSystemPath).ETagEqualsAsync(itemInfo)))
                    {
                        throw new ConflictException(Modified.Server, "Item is modified in the remote storage, ETags not equal.");
                    }
                }

                // Update ETag/LastWriteTime in user file system, so the synchronyzation or remote storage monitor would not start the new update.
                // This is only required to avoid circular updates because of the simplicity of this sample.
                // In your real-life application you will receive a new ETag from server in the update response.
                string eTagNew = newInfo.LastWriteTime.ToUniversalTime().ToString("o");

                await VirtualDrive.GetETagManager(userFileSystemPath).SetETagAsync(eTagNew);

                remoteStorageItem.Attributes = newInfo.Attributes;
                remoteStorageItem.CreationTimeUtc = newInfo.CreationTime.UtcDateTime;
                remoteStorageItem.LastWriteTimeUtc = newInfo.LastWriteTime.UtcDateTime;
                remoteStorageItem.LastAccessTimeUtc = newInfo.LastAccessTime.UtcDateTime;
                remoteStorageItem.LastWriteTimeUtc = newInfo.LastWriteTime.UtcDateTime;

                return eTagNew;
            }
            finally
            {
                Program.VirtualDrive.RemoteStorageMonitor.Enabled = true;
            }
        }

        /// <summary>
        /// Locks the item in the remote storage.
        /// </summary>
        /// <returns>Lock info that conains lock-token returned by the remote storage.</returns>
        /// <remarks>
        /// Lock your item in the remote storage in this method and receive the lock-token.
        /// Return a new <see cref="LockInfo"/> object with the <see cref="LockInfo.LockToken"/> being set from this function.
        /// The <see cref="LockInfo"/> will become available via the <see cref="Lock"/> property when the 
        /// item in the remote storage should be updated. Supply the lock-token during the update request in 
        /// <see cref="VirtualFile.UpdateAsync"/> and <see cref="VirtualFolder.UpdateAsync"/> method calls.
        /// </remarks>
        public async Task<ServerLockInfo> LockAsync()
        {
            return new ServerLockInfo { LockToken = "token", Exclusive = true, LockExpirationDateUtc = DateTimeOffset.Now.AddMinutes(30), Owner = "You" };
        }

        /// <summary>
        /// Unlocks the item in the remote storage.
        /// </summary>
        /// <param name="lockToken">Lock token to unlock the item in the remote storage.</param>
        /// <remarks>
        /// Unlock your item in the remote storage in this method using the 
        /// <paramref name="lockToken"/> parameter.
        /// </remarks>
        public async Task UnlockAsync(string lockToken)
        {

        }
    }
}
