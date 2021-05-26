using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;

namespace VirtualDrive
{
    /// <inheritdoc cref="IFile"/>
    public class VirtualFile : VirtualFileSystemItem, IFile
    {

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="path">File path in the user file system.</param>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFile(string path, VirtualEngine engine, ILogger logger) : base(path, engine, logger)
        {

        }

        /// <inheritdoc/>
        public async Task OpenAsync(IOperationContext operationContext, IResultContext context)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(OpenAsync)}()", UserFileSystemPath);
        }

        /// <inheritdoc/>
        public async Task CloseAsync(IOperationContext operationContext, IResultContext context)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(CloseAsync)}()", UserFileSystemPath);
        }
        
        /// <inheritdoc/>
        public async Task ReadAsync(Stream output,  long offset, long length, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext)
        {
            // On Windows this method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call the resultContext.ReportProgress() or resultContext.ReturnData() method.

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ReadAsync)}({offset}, {length})", UserFileSystemPath);

            SimulateNetworkDelay(length, resultContext);

            await using (FileStream stream = System.IO.File.OpenRead(RemoteStoragePath))
            {
                stream.Seek(offset, SeekOrigin.Begin);
                const int bufferSize = 0x500000; // 5Mb. Buffer size must be multiple of 4096 bytes for optimal performance.
                await stream.CopyToAsync(output, bufferSize, length);
            }
        }

        /// <inheritdoc/>
        public async Task ValidateDataAsync(long offset, long length, IValidateDataOperationContext operationContext, IValidateDataResultContext resultContext)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call the ReturnValidationResult() method or IContextWindows.ReportProgress() method.

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ValidateDataAsync)}({offset}, {length})", UserFileSystemPath);

            //SimulateNetworkDelay(length, resultContext);

            bool isValid = true;

            resultContext.ReturnValidationResult(offset, length, isValid);
        }

        /// <inheritdoc/>
        public async Task WriteAsync(IFileMetadata fileMetadata, Stream content = null)
        {
            if(MsOfficeHelper.IsMsOfficeLocked(UserFileSystemPath)) // Required for PowerPoint. It does not block the for writing.
            {
                throw new ClientLockFailedException("The file is blocked for writing.");
            }

            Logger.LogMessage($"{nameof(IFile)}.{nameof(WriteAsync)}()", UserFileSystemPath);

            CustomDataManager customDataManager = Engine.CustomDataManager(UserFileSystemPath);
            // Send the ETag to the server as part of the update to ensure the file in the remote storge is not modified since last read.
            string oldEtag = await customDataManager.ETagManager.GetETagAsync();

            // Send the lock-token to the server as part of the update.
            string lockToken = (await customDataManager.LockManager.GetLockInfoAsync())?.LockToken;

            FileInfo remoteStorageItem = new FileInfo(RemoteStoragePath);

            if (content != null)
            {
                // Upload remote storage file content.
                await using (FileStream remoteStorageStream = remoteStorageItem.Open(FileMode.Open, FileAccess.Write, FileShare.Delete))
                {
                    await content.CopyToAsync(remoteStorageStream);
                    remoteStorageStream.SetLength(content.Length);
                }
            }

            // Update remote storage file metadata.
            remoteStorageItem.Attributes = fileMetadata.Attributes;
            remoteStorageItem.CreationTimeUtc = fileMetadata.CreationTime.UtcDateTime;
            remoteStorageItem.LastWriteTimeUtc = fileMetadata.LastWriteTime.UtcDateTime;
            remoteStorageItem.LastAccessTimeUtc = fileMetadata.LastAccessTime.UtcDateTime;
            remoteStorageItem.LastWriteTimeUtc = fileMetadata.LastWriteTime.UtcDateTime;

            // Get the new ETag from server here as part of the update and save it on the client.
            string newEtag = "1234567890";
            await customDataManager.ETagManager.SetETagAsync(newEtag);
            await customDataManager.SetCustomColumnsAsync(new[] { new FileSystemItemPropertyData((int)CustomColumnIds.ETag, newEtag) });
        }
    }
}
