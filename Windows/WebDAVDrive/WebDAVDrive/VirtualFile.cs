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
        /// <param name="path">File path in the user file system.</param>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFile(string path, VirtualEngine engine, ILogger logger) : base(path, engine, logger)
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
            // To process longer requests and reset the timout timer call the resultContext.ReportProgress() or resultContext.ReturnData() method.

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ReadAsync)}({offset}, {length})", UserFileSystemPath, default, operationContext);

            if (offset == 0 && length == operationContext.FileSize)
            {
                // If we read entire file, do not add Range header. Pass -1 to not add it.
                offset = -1;
            }

            string eTag = null;

            // Buffer size must be multiple of 4096 bytes for optimal performance.
            const int bufferSize = 0x500000; // 5Mb.
            using (Client.IWebResponse response = await Program.DavClient.DownloadAsync(new Uri(RemoteStoragePath), offset, length, cancellationToken))
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
                eTag = response.GetHeaderValue("ETag");
            }

            //// Store ETag here.
            PlaceholderItem placeholder = Engine.Placeholders.GetItem(UserFileSystemPath);
            await placeholder.Properties.AddOrUpdateAsync("ETag", eTag);

            //using (Stream stream = await Program.DavClient.DownloadAsync(new Uri(RemoteStoragePath), offset, length))
            //{
            //    await stream.CopyToAsync(output, bufferSize, length);
            //}
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

                string oldEtag = null;

                if (placeholder.Properties.TryGetValue("ETag", out IDataItem propETag))
                {
                    propETag.TryGetValue<string>(out oldEtag);
                }

                // Read the lock-token and send it to the server as part of the update.
                Client.LockUriTokenPair[] lockTokens = null;
                IDataItem propLockInfo;
                if (placeholder.Properties.TryGetValue("LockInfo", out propLockInfo))
                {
                    ServerLockInfo lockInfo;
                    if (propLockInfo.TryGetValue<ServerLockInfo>(out lockInfo))
                    {
                        string lockToken = lockInfo.LockToken;
                        lockTokens = new Client.LockUriTokenPair[] { new Client.LockUriTokenPair(new Uri(RemoteStoragePath), lockToken) };
                    }
                }

                // Update remote storage file content,
                // also get and save a new ETag returned by the server, if any.
                string newEtag = await Program.DavClient.UploadAsync(new Uri(RemoteStoragePath), async (outputStream) =>
                {
                    // Setting position to 0 is required in case of retry.
                    content.Position = 0;
                    await content.CopyToAsync(outputStream);
                }, null, content.Length, 0, -1, lockTokens, oldEtag, cancellationToken);

                await placeholder.Properties.AddOrUpdateAsync("ETag", newEtag);
            }
        }
    }
}
