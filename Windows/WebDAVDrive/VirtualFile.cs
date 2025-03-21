using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;
using Client = ITHit.WebDAV.Client;


namespace WebDAVDrive
{
    /// <inheritdoc cref="IFile"/>
    public class VirtualFile : VirtualFileSystemItem, IFileWindows
    {

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="remoteStorageId">Remote storage item ID.</param>
        /// <param name="userFileSystemPath">User file system path. This paramater is available on Windows platform only. On macOS and iOS this parameter is always null</param>
        /// <param name="engine">Engine instance.</param>
        /// <param name="autoLockTimeoutMs">Automatic lock timeout in milliseconds.</param>
        /// <param name="manualLockTimeoutMs">Manual lock timeout in milliseconds.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFile(byte[] remoteStorageId, string userFileSystemPath, VirtualEngine engine, double autoLockTimeoutMs, double manualLockTimeoutMs, AppSettings appSettings, ILogger logger) 
            : base(remoteStorageId, userFileSystemPath, engine, autoLockTimeoutMs, manualLockTimeoutMs, appSettings, logger)
        {

        }

        /// <inheritdoc/>
        public async Task OpenCompletionAsync(IOperationContext operationContext, IResultContext context, CancellationToken cancellationToken)
        {
            Logger.LogDebug($"{nameof(IFileWindows)}.{nameof(OpenCompletionAsync)}()", UserFileSystemPath, default, operationContext);
        }

        /// <inheritdoc/>
        public async Task CloseCompletionAsync(IOperationContext operationContext, IResultContext context, CancellationToken cancellationToken)
        {
            Logger.LogDebug($"{nameof(IFileWindows)}.{nameof(CloseCompletionAsync)}()", UserFileSystemPath, default, operationContext);
        }

        /// <inheritdoc/>
        public async Task<IFileMetadata> ReadAsync(Stream output, long offset, long length, IFileMetadata metadata, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext, CancellationToken cancellationToken)
        {
            // On Windows this method has a 60 sec timeout. 
            // To process longer requests and reset the timeout timer write to the output stream or call the resultContext.ReportProgress() or resultContext.ReturnData() methods.

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ReadAsync)}({offset}, {length})", UserFileSystemPath, default, operationContext, metadata);

            if (offset == 0 && length == operationContext.FileSize)
            {
                // If we read entire file we do not add a Range header.
                offset = -1;
            }

            // Send content eTag to the server in download request. 
            // This will ensure file content did not change since eTag received from server.
            string contentETag = metadata.ContentETag;

            // Buffer size must be multiple of 4096 bytes for optimal performance.
            const int bufferSize = 0x500000; // 5Mb.
            using (Client.IDownloadResponse response = await Dav.DownloadAsync(new Uri(RemoteStoragePath), offset, length, null, cancellationToken))
            {
                using (Stream stream = await response.GetResponseStreamAsync())
                {
                    try
                    {
                        await stream.CopyToAsync(output, bufferSize, length, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Operation was canceled by the calling Engine.StopAsync() or the operation timeout occured.
                        Logger.LogMessage($"{nameof(ReadAsync)}({offset}, {length}) canceled", UserFileSystemPath, default, operationContext, metadata);
                    }
                }
                // Return content eTag to the Engine.
                contentETag = response.Headers.ETag.Tag;
            }

            // Return an updated item to the Engine.
            // In the returned data set the following fields:
            //  - Content eTag. The Engine will store it to determine if the file content should be updated.
            //  - Medatdata eTag. The Engine will store it to determine if the item metadata should be updated.
            return new FileMetadata()
            {
                ContentETag = contentETag
                //MetadataETag = 
            };
        }

        /// <inheritdoc/>
        public async Task ValidateDataAsync(long offset, long length, IValidateDataOperationContext operationContext, IValidateDataResultContext resultContext)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timeout timer call the ReturnValidationResult()
            // method or IResultContext.ReportProgress() method.

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ValidateDataAsync)}({offset}, {length})", UserFileSystemPath, default, operationContext);

            bool isValid = true;

            resultContext.ReturnValidationResult(offset, length, isValid);
        }

        /// <inheritdoc/>
        public async Task<IFileMetadata> WriteAsync(IFileMetadata metadata, Stream content = null, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            long contentLength = content != null ? content.Length : 0;
            Logger.LogMessage($"{nameof(IFile)}.{nameof(WriteAsync)}({contentLength})", UserFileSystemPath, default, operationContext, metadata);

            string newContentEtag = null;
            if (content != null)
            {
                // Send the ETag to the server as part of the update to ensure
                // the file in the remote storge is not modified since last read.
                string oldContentEtag = metadata.ContentETag;

                Client.LockUriTokenPair[] lockTokens = null;
                // Read the lock-token and send it to the server as part of the update.
                // Send the lock-token only in case the item is locked by this user.
                if (operationContext.Properties.TryGetCurrentUserLockToken(out string lockToken))
                {
                    lockTokens = new Client.LockUriTokenPair[] { new Client.LockUriTokenPair(new Uri(RemoteStoragePath), lockToken) };
                }

                try
                {
                    // Update remote storage file content.
                    Client.IWebDavResponse<string> response = await Dav.UploadAsync(new Uri(RemoteStoragePath), async (outputStream) =>
                    {
                        content.Position = 0; // Setting position to 0 is required in case of retry.
                        await content.CopyToAsync(outputStream, cancellationToken);
                    }, null, content.Length, 0, -1, lockTokens, oldContentEtag, null, cancellationToken);

                    // Return new content eTag back to the Engine.
                    newContentEtag = response.WebDavResponse;

                    if (string.IsNullOrEmpty(newContentEtag))
                    {
                        Logger.LogError("The server did not return ETag after update.", UserFileSystemPath, null, null, operationContext, metadata);
                    }
                }
                catch (Client.Exceptions.LockedException ex)
                {
                    // The item is locked on the server and the client did not provide a lock token.
                    // Here do NOT set conflict status because this item may be uploaded
                    // later automatically, when server item is unlocked.
                    //placeholder.SetConflictStatus(true);

                    Logger.LogMessage($"Upload failed. The item is locked", UserFileSystemPath, default, operationContext, metadata);
                    inSyncResultContext.SetInSync = false;
                    inSyncResultContext.Result = new OperationResult(OperationStatus.Locked, 0, "Upload failed. The item is locked", ex);
                }
                catch (Client.Exceptions.PreconditionFailedException ex)
                {
                    // Server and client content ETags do not match.
                    // Here we set conflict status in Windows Explorer becuse this item can
                    // NOT be uploaded automatically. The conflict must be resolved first.

                    Logger.LogMessage($"Conflict. The item is modified", UserFileSystemPath, default, operationContext, metadata);
                    Engine.Placeholders.GetFile(UserFileSystemPath).SetConflictStatus(true);
                    inSyncResultContext.SetInSync = false;
                    inSyncResultContext.Result = new OperationResult(OperationStatus.Conflict, 0, "Conflict. The item is modified", ex);
                }
            }

            // Return an updated item to the Engine.
            // In the returned data set the following fields:
            //  - Content eTag. The Engine will store it to determine if the file content should be updated.
            //  - Medatdata eTag. The Engine will store it to determine if the item metadata should be updated.
            return new FileMetadata()
            {
                ContentETag = newContentEtag
                //MetadataETag = 
            };
        }
    }

    class MyOperationStatus 
    {
        
    }
}
