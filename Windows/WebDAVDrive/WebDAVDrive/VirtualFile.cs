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
using Client = ITHit.WebDAV.Client;

namespace WebDAVDrive
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
            Logger.LogMessage($"{nameof(IFile)}.{nameof(OpenAsync)}()", UserFileSystemPath, default, operationContext);
        }

        /// <inheritdoc/>
        public async Task CloseAsync(IOperationContext operationContext, IResultContext context)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(CloseAsync)}()", UserFileSystemPath, default, operationContext);
        }
        
        /// <inheritdoc/>
        public async Task ReadAsync(Stream output, long offset, long length, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext)
        {
            // On Windows this method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call the resultContext.ReportProgress() or resultContext.ReturnData() method.

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ReadAsync)}({offset}, {length})", UserFileSystemPath, default, operationContext);

            SimulateNetworkDelay(length, resultContext);

            if (offset == 0 && length == operationContext.FileSize) {
                // If we read entire file, do not add Range header. Pass -1 to not add it.
                offset = -1;
            }
            using (Stream stream = await Program.DavClient.FileReadAsync(new Uri(RemoteStoragePath), offset, length))
            {
                const int bufferSize = 0x500000; // 5Mb. Buffer size must be multiple of 4096 bytes for optimal performance.
                await stream.CopyToAsync(output, bufferSize, length);
            }
        }

        /// <inheritdoc/>
        public async Task ValidateDataAsync(long offset, long length, IValidateDataOperationContext operationContext, IValidateDataResultContext resultContext)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call the ReturnValidationResult() method or IContextWindows.ReportProgress() method.

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ValidateDataAsync)}({offset}, {length})", UserFileSystemPath, default, operationContext);

            //SimulateNetworkDelay(length, resultContext);

            bool isValid = true;

            resultContext.ReturnValidationResult(offset, length, isValid);
        }

        /// <inheritdoc/>
        public async Task WriteAsync(IFileMetadata fileMetadata, Stream content = null, IOperationContext operationContext = null)
        {
            if(MsOfficeHelper.IsMsOfficeLocked(UserFileSystemPath)) // Required for PowerPoint. It does not block the file for writing.
            {
                throw new ClientLockFailedException("The file is blocked for writing.");
            }

            Logger.LogMessage($"{nameof(IFile)}.{nameof(WriteAsync)}()", UserFileSystemPath, default, operationContext);

            ExternalDataManager customDataManager = Engine.CustomDataManager(UserFileSystemPath);
            // Send the ETag to the server as part of the update to ensure the file in the remote storge is not modified since last read.
            string oldEtag = await customDataManager.ETagManager.GetETagAsync();

            // Send the lock-token to the server as part of the update.
            string lockToken = (await customDataManager.LockManager.GetLockInfoAsync())?.LockToken;
            Client.LockUriTokenPair[] lockTokens = new Client.LockUriTokenPair[] { new Client.LockUriTokenPair(new Uri(RemoteStoragePath), lockToken) };

            if (content != null)
            {
                long contentLength = content != null ? content.Length : 0;

                // Update remote storage file content.
                // Get the new ETag returned by the server (if any).
                string eTagNew = await Program.DavClient.FileWriteAsync(new Uri(RemoteStoragePath), async (outputStream) => {
                    if (content != null)
                    {
                        // Rewind for new copy (e.g. retry)
                        content.Position = 0;
                        await content.CopyToAsync(outputStream);
                    }
                }, null, contentLength, 0, -1, lockTokens, oldEtag);

                // Store ETag unlil the next update.
                // This will also mark the item as not new, which is required for correct MS Office saving opertions.
                await customDataManager.ETagManager.SetETagAsync(eTagNew);

                // Update ETag in custom column displayed in file manager.
                await customDataManager.SetCustomColumnsAsync(new[] { new FileSystemItemPropertyData((int)CustomColumnIds.ETag, eTagNew) });
            }
        }
    }
}
