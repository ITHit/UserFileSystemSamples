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
    // In most cases you can use this class in your project without any changes.
    /// <inheritdoc cref="IFile"/>
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
            Logger.LogMessage("IFile.OpenAsync()", UserFileSystemPath);

            // Auto-lock the file.
            string userFileSystemFilePath = UserFileSystemPath;
            if (Engine.ChangesProcessingEnabled && FsPath.Exists(userFileSystemFilePath))
            {
                if (Program.Settings.AutoLock
                    && !FsPath.AvoidAutoLock(userFileSystemFilePath) 
                    && ! await Lock.IsLockedAsync(userFileSystemFilePath)
                    && FsPath.IsWriteLocked(userFileSystemFilePath)
                    && !new PlaceholderFile(userFileSystemFilePath).IsNew())
                {
                    try
                    {
                        await new RemoteStorageRawItem(userFileSystemFilePath, Logger).LockAsync(LockMode.Auto);
                    }
                    catch(ClientLockFailedException ex)
                    {
                        // Lock file is blocked by the concurrent thread. This is a normal behaviour.
                        Logger.LogMessage(ex.Message, userFileSystemFilePath);
                    }
                }
            }
        }

        //$<IFolder.CloseAsync
        /// <inheritdoc/>
        public async Task CloseAsync(IOperationContext operationContext, IResultContext context)
        {
            // Here, if the file in the user file system is modified (not in-sync), you will send the file content, 
            // creation time, modification time and attributes to the remote storage.
            // We also send ETag, to make sure the changes on the server, if any, are not overwritten.

            Logger.LogMessage("IFile.CloseAsync()", UserFileSystemPath);

            string userFileSystemFilePath = UserFileSystemPath;

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
                    Logger.LogMessage("Converted to placeholder", userFileSystemFilePath);
                }
                
                try
                {
                    if (PlaceholderItem.GetItem(userFileSystemFilePath).IsNew())
                    {
                        // Create new file in the remote storage.
                        await RemoteStorageRawItem.CreateAsync(userFileSystemFilePath, Logger);
                    }
                    else
                    {
                        // Send content to remote storage. Unlock if auto-locked.
                        await new RemoteStorageRawItem(userFileSystemFilePath, Logger).UpdateAsync();
                    }
                }
                catch (IOException ex)
                {
                        // Either the file is already being synced in another thread or client or server file is blocked by concurrent process.
                        // This is a normal behaviour.
                        // The file must be synched by your synchronyzation service at a later time, when the file becomes available.
                        Logger.LogMessage("Failed to upload file. Possibly in use by an application or blocked for synchronization in another thread:", ex.Message);
                }                
            }
        }
        //$>

        //$<IFile.TransferDataAsync
        /// <inheritdoc/>
        public async Task TransferDataAsync(long offset, long length, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call resultContext.ReportProgress() method.

            Logger.LogMessage($"IFile.TransferDataAsync({offset}, {length})", UserFileSystemPath);

            SimulateNetworkDelay(length, resultContext);
            
            long optionalLength = length + operationContext.OptionalLength;

            byte[] buffer = await new UserFile(UserFileSystemPath).ReadAsync(offset, optionalLength);

            resultContext.ReturnData(buffer, offset, optionalLength);
        }
        //$>

        /// <inheritdoc/>
        public async Task ValidateDataAsync(long offset, long length, IValidateDataOperationContext operationContext, IValidateDataResultContext resultContext)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call IContextWindows.ReportProgress() method.

            Logger.LogMessage($"IFile.ValidateDataAsync({offset}, {length})", UserFileSystemPath);

            //SimulateNetworkDelay(length, resultContext);

            bool isValid = await new UserFile(UserFileSystemPath).ValidateDataAsync(offset, length);

            resultContext.ReturnValidationResult(offset, length, isValid);
        }
    }
}
