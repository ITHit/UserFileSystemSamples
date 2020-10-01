using ITHit.FileSystem;
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
            try
            {
                if (FsPath.IsFile(userFileSystemPath))
                {
                    await new RemoteStorageItem(remoteStoragePath).CreateOrUpdateFileAsync(userFileSystemPath, true);
                }
                else
                {
                    await new RemoteStorageItem(remoteStoragePath).CreateOrUpdateFolderAsync(userFileSystemPath, true);
                }
                await new UserFileSystemItem(userFileSystemPath).ClearStateAsync();

            }
            catch(Exception ex)
            {
                await new UserFileSystemItem(userFileSystemPath).SetUploadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }

        public async Task UpdateAsync(string userFileSystemPath)
        {
            try
            {
                if (FsPath.IsFile(userFileSystemPath))
                {
                    await CreateOrUpdateFileAsync(userFileSystemPath, false);
                }
                else
                {
                    await CreateOrUpdateFolderAsync(userFileSystemPath, false);
                }
                await new UserFileSystemItem(userFileSystemPath).ClearStateAsync();
            }
            catch(Exception ex)
            {
                await new UserFileSystemItem(userFileSystemPath).SetUploadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
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

                using (WindowsFileSystemItem userFileSystemWinItem = WindowsFileSystemItem.OpenReadAttributes(userFileSystemPath, FileMode.Open, FileShare.Read))
                //await using (FileStream userFileSystemStream = userFileSystemFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // Create the new file in the remote storage only if the file in the user file system was not moved.
                    if (create && PlaceholderItem.GetItem(userFileSystemPath).IsMoved())
                    {
                        string originalPath = PlaceholderItem.GetItem(userFileSystemPath).GetOriginalPath();
                        throw new ConflictException(Modified.Client, $"The item was moved. Original path: {originalPath}");
                    }

                    // If another thread is trying to sync the same file, this call will fail in other threads.
                    // In your implemntation you must lock your remote storage file, or block it for reading and writing by other means.
                    await using (FileStream remoteStorageStream = remoteStorageFile.Open(fileMode, FileAccess.Write, FileShare.None))
                    {
                        userFileSystemFile.Refresh(); // Ensures LastWriteTimeUtc is in sync with file content after Open() was called.


                        if (!create)
                        {
                            // Verify that the file in remote storage is not modified since it was downloaded to user file system.
                            if ( !(await new UserFileSystemItem(userFileSystemPath).ETagEqualsAsync(remoteStorageFile)))
                            {
                                throw new ConflictException(Modified.Server, "Item is modified in the remote storage, ETags not equal.");
                            }
                        }

                        // Update ETag/LastWriteTime in user file system, so the synchronyzation or remote storage monitor would not start the new update.
                        // This is only required to avoid circular updates because of the simplicity of this sample.
                        PlaceholderItemExtensions.SetCustomData(userFileSystemWinItem.SafeHandle,
                            userFileSystemFile.LastWriteTime.ToBinary().ToString(), userFileSystemPath);

                        // Update remote storage file content.
                        // File is marked as not in-sync when moved. Opening a file for reading triggers hydration, open only ir content is modified.
                        if (PlaceholderFile.GetFileDataSizeInfo(userFileSystemWinItem.SafeHandle).ModifiedDataSize > 0)
                        {
                            await using (FileStream userFileSystemStream = userFileSystemFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                await userFileSystemStream.CopyToAsync(remoteStorageStream);
                                remoteStorageStream.SetLength(userFileSystemStream.Length);
                            }
                        }

                        // Remove Pinned, unpinned and offline flags.
                        FileAttributes flags = userFileSystemFile.Attributes
                            & (FileAttributes)~FileAttributesExt.Pinned
                            & (FileAttributes)~FileAttributesExt.Unpinned
                            & (FileAttributes)~FileAttributesExt.Offline;

                        // Update remote storage file basic info.
                        WindowsFileSystemItem.SetFileInformation(remoteStorageStream.SafeFileHandle, flags,
                                    userFileSystemFile.CreationTimeUtc,
                                    userFileSystemFile.LastWriteTimeUtc,
                                    userFileSystemFile.LastAccessTimeUtc,
                                    userFileSystemFile.LastWriteTimeUtc);

                        // Here you will set a new ETag returned by the remote storage.

                        PlaceholderItem.SetInSync(userFileSystemWinItem.SafeHandle, true);
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

                // Create the new file in the remote storage only only if the file in the user file system was not moved.
                if (create && PlaceholderItem.GetItem(userFileSystemPath).IsMoved())
                {
                    string originalPath = PlaceholderItem.GetItem(userFileSystemPath).GetOriginalPath();
                    throw new ConflictException(Modified.Client, $"The item was moved. Original path: {originalPath}");
                }

                DirectoryInfo remoteStorageFolder = new DirectoryInfo(remoteStoragePath);

                remoteStorageFolder.Create();

                // Update ETag/LastWriteTime in user file system, so the synchronyzation or remote storage monitor would not start the new update.
                byte[] customData = new CustomData 
                {
                    ETag = userFileSystemFolder.LastWriteTime.ToBinary().ToString(),
                    OriginalPath = userFileSystemPath
                }.Serialize();
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

        internal async Task MoveToAsync(string userFileSystemNewPath, IConfirmationResultContext resultContext = null)
        {
            string userFileSystemOldPath = Mapping.ReverseMapPath(this.remoteStoragePath);

            try
            {
                    bool? inSync = null;
                    bool updateTargetOnSuccess = false;
                    try
                    {
                        if (!FsPath.IsRecycleBin(userFileSystemNewPath) // When a file is deleted, it is moved to a Recycle Bin.
                            && !FsPath.AvoidSync(userFileSystemOldPath) && !FsPath.AvoidSync(userFileSystemNewPath))
                        {
                            if (FsPath.Exists(userFileSystemOldPath))
                            {
                                inSync = PlaceholderItem.GetItem(userFileSystemOldPath).GetInSync();
                            }

                            string remoteStorageOldPath = Mapping.MapPath(userFileSystemOldPath);
                            string remoteStorageNewPath = Mapping.MapPath(userFileSystemNewPath);

                            FileSystemInfo remoteStorageOldItem = FsPath.GetFileSystemItem(remoteStorageOldPath);
                            if (remoteStorageOldItem != null)
                            {
                                Program.RemoteStorageMonitorInstance.Enabled = false; // Disable RemoteStorageMonitor to avoid circular calls.

                                if (remoteStorageOldItem is FileInfo)
                                {
                                    (remoteStorageOldItem as FileInfo).MoveTo(remoteStorageNewPath, false);
                                }
                                else
                                {
                                    (remoteStorageOldItem as DirectoryInfo).MoveTo(remoteStorageNewPath);
                                }
                            }
                            updateTargetOnSuccess = true;
                        }
                    }
                    finally
                    {
                        if (resultContext != null)
                        {
                            resultContext.ReturnConfirmationResult();
                        }

                        if (updateTargetOnSuccess && PlaceholderItem.IsPlaceholder(userFileSystemNewPath))
                        {
                            // Update OriginalPath, so the item does not appear as moved.
                            PlaceholderItem.GetItem(userFileSystemNewPath).SetOriginalPath(userFileSystemNewPath);

                            // If a file with content is deleted it is moved to a recycle bin and converted
                            // to a regular file, so placeholder features are not available on it, checking if a file is a placeholder.
                            if (inSync != null)
                            {
                                PlaceholderItem.GetItem(userFileSystemNewPath).SetInSync(inSync.Value);
                            }

                            await new UserFileSystemItem(userFileSystemNewPath).ClearStateAsync();
                        }
                    }
            }
            catch (Exception ex)
            {
                string userFileSystemPath = FsPath.Exists(userFileSystemNewPath) ? userFileSystemNewPath : userFileSystemOldPath;
                await new UserFileSystemItem(userFileSystemPath).SetUploadErrorStateAsync(ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
            finally
            {
                Program.RemoteStorageMonitorInstance.Enabled = true;
            }
        }
    }
}
