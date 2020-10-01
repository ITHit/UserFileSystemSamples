using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VirtualFileSystem.Syncronyzation;

namespace VirtualFileSystem
{
    /// <inheritdoc/>
    internal class VfsFile : VfsFileSystemItem, IFile
    {

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="path">File path in user file system.</param>
        /// <param name="logger">Logger.</param>
        public VfsFile(string path, ILogger logger, VfsEngine engine) : base(path, logger, engine)
        {

        }

        /// <inheritdoc/>
        public async Task OpenAsync(IOperationContext operationContext, IResultContext context)
        {
            LogMessage("IFile.OpenAsync()", this.FullPath);
        }

        /// <inheritdoc/>
        public async Task CloseAsync(IOperationContext operationContext, IResultContext context)
        {
            // Here, if the file in user file system is modified (not in-sync), we send file content, 
            // creation time, modification time and attributes to remote storage.
            // We also create new ETag, associate it with a file in user file system and send it to the server.

            LogMessage("IFile.CloseAsync()", this.FullPath);

            string userFileSystemFilePath = this.FullPath;

            // In case the file is moved it does not exist in user file system when CloseAsync() is called.
            if (Engine.ChangesProcessingEnabled
                && FsPath.Exists(userFileSystemFilePath)
                && !FsPath.AvoidSync(userFileSystemFilePath))
            {

                // In case the file is overwritten it is converted to a regular file prior to CloseAsync().
                // we need to convert it back into file/folder placeholder.
                if (!PlaceholderItem.IsPlaceholder(userFileSystemFilePath))
                {
                    PlaceholderItem.ConvertToPlaceholder(userFileSystemFilePath, false);
                    LogMessage("Converted to placeholder:", userFileSystemFilePath);
                }

                if (!PlaceholderItem.GetItem(userFileSystemFilePath).GetInSync())
                {
                    LogMessage("Changed:", userFileSystemFilePath);

                    string remoteStorageFilePath = Mapping.MapPath(userFileSystemFilePath);

                    try
                    {
                        await new RemoteStorageItem(remoteStorageFilePath).UpdateAsync(userFileSystemFilePath);
                        LogMessage("Updated succesefully:", remoteStorageFilePath);
                    }
                    catch (IOException ex)
                    {
                        // Either the file is already being synced in another thread or client or server file is blocked by concurrent process.
                        // This is a normal behaviour.
                        // The file must be synched by your synchronyzation service at a later time, when the file becomes available.
                        LogMessage("Failed to upload file. Possibly in use by an application or blocked for synchronization in another thread:", ex.Message);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public async Task TransferDataAsync(long offset, long length, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call IContextWindows.ReportProgress() method.

            LogMessage($"IFile.TransferDataAsync({offset}, {length})", this.FullPath);

            SimulateNetworkDelay(length, resultContext);

            
            string remoteStoragePath = Mapping.MapPath(this.FullPath);

            // Transfering file content.
            await using (FileStream stream = File.OpenRead(remoteStoragePath))
            {
                stream.Seek(offset, SeekOrigin.Begin);
                byte[] buffer = new byte[length];
                int bytesRead = await stream.ReadAsync(buffer, 0, (int)length);
                resultContext.ReturnData(buffer, offset, length);
            }
        }

        /// <inheritdoc/>
        public async Task ValidateDataAsync(long offset, long length, IValidateDataOperationContext operationContext, IValidateDataResultContext resultContext)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call IContextWindows.ReportProgress() method.

            LogMessage($"IFile.ValidateDataAsync({offset}, {length})", this.FullPath);

            SimulateNetworkDelay(length, resultContext);

            resultContext.ReturnValidationResult(offset, length, true);
        }
    }
}
