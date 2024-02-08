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
        /// <param name="autoLockTimoutMs">Automatic lock timout in milliseconds.</param>
        /// <param name="manualLockTimoutMs">Manual lock timout in milliseconds.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFile(byte[] remoteStorageId, string userFileSystemPath, VirtualEngine engine, double autoLockTimoutMs, double manualLockTimoutMs, ILogger logger) 
            : base(remoteStorageId, userFileSystemPath, engine, autoLockTimoutMs, manualLockTimoutMs, logger)
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
        public async Task ReadAsync(Stream output, long offset, long length, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext, CancellationToken cancellationToken)
        {
            // On Windows this method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer write to the output stream or call the resultContext.ReportProgress() or resultContext.ReturnData() methods.

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ReadAsync)}({offset}, {length})", UserFileSystemPath, default, operationContext);

            if (offset == 0 && length == operationContext.FileSize)
            {
                // If we read entire file we do not add a Range header.
                offset = -1;
            }

            string eTag = null;

            // Buffer size must be multiple of 4096 bytes for optimal performance.
            const int bufferSize = 0x500000; // 5Mb.
            using (Client.IDownloadResponse response = await Program.DavClient.DownloadAsync(new Uri(RemoteStoragePath), offset, length, null, cancellationToken))
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
                        Logger.LogMessage($"{nameof(ReadAsync)}({offset}, {length}) canceled", UserFileSystemPath, default);
                    }
                }
                eTag = response.Headers.ETag.Tag;
            }

            // Store ETag here.
            operationContext.Properties.SetETag(eTag);
        }

        /// <inheritdoc/>
        public async Task ValidateDataAsync(long offset, long length, IValidateDataOperationContext operationContext, IValidateDataResultContext resultContext)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call the ReturnValidationResult()
            // method or IResultContext.ReportProgress() method.

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ValidateDataAsync)}({offset}, {length})", UserFileSystemPath, default, operationContext);

            bool isValid = true;

            resultContext.ReturnValidationResult(offset, length, isValid);
        }

        /// <inheritdoc/>
        public async Task<IFileMetadata> WriteAsync(IFileSystemBasicInfo fileBasicInfo, Stream content = null, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(WriteAsync)}()", UserFileSystemPath, default, operationContext);

            if (content != null)
            {
                // Send the ETag to the server as part of the update to ensure
                // the file in the remote storge is not modified since last read.
                operationContext.Properties.TryGetETag(out string oldEtag);

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
                    Client.IWebDavResponse<string> response = await Program.DavClient.UploadAsync(new Uri(RemoteStoragePath), async (outputStream) =>
                    {
                        content.Position = 0; // Setting position to 0 is required in case of retry.
                        await content.CopyToAsync(outputStream, cancellationToken);
                    }, null, content.Length, 0, -1, lockTokens, oldEtag, null, cancellationToken);

                    // Save a new ETag returned by the server, if any.
                    string newEtag = response.WebDavResponse;

                    if (string.IsNullOrEmpty(newEtag))
                    {
                        Logger.LogError("The server did not return ETag after update.", UserFileSystemPath, null, null, operationContext);
                    }
                    operationContext.Properties.SetETag(newEtag);
                }
                catch (Client.Exceptions.LockedException)
                {
                    // The item is locked on the server and the client did not provide a lock token.
                    // We do not set the conflict status.
                    // This item may be uploaded later automatically, when server item is unlocked.
                    //placeholder.SetConflictStatus(true);

                    Logger.LogMessage($"Upload failed. The item is locked", UserFileSystemPath, default, operationContext);
                    inSyncResultContext.SetInSync = false;
                }
                catch (Client.Exceptions.PreconditionFailedException)
                {
                    // Server and client ETags do not match.
                    // Set conflict status in Windows Explorer.
                    // This item can not be uploaded automatically, conflict needs to be resolved first.

                    Logger.LogMessage($"Conflict. The item is modified", UserFileSystemPath, default, operationContext);
                    Engine.Placeholders.GetItem(UserFileSystemPath).SetConflictStatus(true);
                    inSyncResultContext.SetInSync = false;
                }
            }

            return null;
        }
    }
}
