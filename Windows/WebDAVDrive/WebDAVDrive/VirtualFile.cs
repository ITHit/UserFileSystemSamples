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
            Engine.Placeholders.GetItem(UserFileSystemPath).SetETag(eTag);
        }

        /// <inheritdoc/>
        public async Task ValidateDataAsync(long offset, long length, IValidateDataOperationContext operationContext, IValidateDataResultContext resultContext)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call the ReturnValidationResult() method or IContextWindows.ReportProgress() method.

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ValidateDataAsync)}({offset}, {length})", UserFileSystemPath, default, operationContext);

            bool isValid = true;

            resultContext.ReturnValidationResult(offset, length, isValid);
        }

        /// <inheritdoc/>
        public async Task WriteAsync(IFileMetadata fileMetadata, Stream content = null, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(WriteAsync)}()", UserFileSystemPath, default, operationContext);

            if (content != null)
            {
                // Send the ETag to the server as part of the update to ensure
                // the file in the remote storge is not modified since last read.
                PlaceholderItem placeholder = Engine.Placeholders.GetItem(UserFileSystemPath);
                
                placeholder.TryGetETag(out string oldEtag);

                // Read the lock-token and send it to the server as part of the update.
                Client.LockUriTokenPair[] lockTokens = null;
                if (placeholder.TryGetLockInfo(out ServerLockInfo lockInfo))
                {
                    // Send the lock-token only in case the item is locked by this user.
                    bool thisUser = Engine.CurrentUserPrincipal.Equals(lockInfo.Owner, StringComparison.InvariantCultureIgnoreCase);
                    if (thisUser)
                    {
                        string lockToken = lockInfo.LockToken;
                        lockTokens = new Client.LockUriTokenPair[] { new Client.LockUriTokenPair(new Uri(RemoteStoragePath), lockToken) };
                    }
                }

                try
                {
                    // Update remote storage file content.
                    Client.IWebDavResponse<string> response = await Program.DavClient.UploadAsync(new Uri(RemoteStoragePath), async (outputStream) =>
                    {
                        content.Position = 0; // Setting position to 0 is required in case of retry.
                        await content.CopyToAsync(outputStream);
                    }, null, content.Length, 0, -1, lockTokens, oldEtag, null, cancellationToken);

                    // Save a new ETag returned by the server, if any.
                    placeholder.SetETag(response.WebDavResponse);
                }
                catch (Client.Exceptions.PreconditionFailedException)
                {
                    // Server and client ETags do not match.
                    // Set conflict status in Windows Explorer.

                    Logger.LogMessage($"Conflict. The item is modified.", UserFileSystemPath, default, operationContext);
                    placeholder.SetErrorStatus(true);
                    inSyncResultContext.SetInSync = false;
                }
            }
        }
    }
}
