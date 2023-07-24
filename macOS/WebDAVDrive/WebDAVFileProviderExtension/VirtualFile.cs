using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileProviderExtension.Extensions;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;
using Client = ITHit.WebDAV.Client;

namespace WebDAVFileProviderExtension
{
    /// <inheritdoc cref="IFile"/>
    public class VirtualFile : VirtualFileSystemItem, IFile
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="remoteStorageId">Id uri on the WebDav server.</param>
        /// <param name="session">WebDAV session.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFile(byte[] remoteStorageId, Client.WebDavSession session, ILogger logger) : base(remoteStorageId, session, logger)
        {

        }
     
        /// <inheritdoc/>
        public async Task ReadAsync(Stream output, long offset, long length, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext, CancellationToken cancellationToken)
        {           
            Logger.LogMessage($"{nameof(IFile)}.{nameof(ReadAsync)}({offset}, {length})", RemoteStorageUriById.AbsoluteUri, default, operationContext);

            // Buffer size must be multiple of 4096 bytes for optimal performance.
            const int bufferSize = 0x500000; // 5Mb.
            using (Client.IDownloadResponse response = await Session.DownloadAsync(new Uri(RemoteStorageUriById.AbsoluteUri), offset, length, null, cancellationToken))
            {
                using (Stream stream = await response.GetResponseStreamAsync())
                {
                    try
                    {
                        Logger.LogMessage("Start download.");
                        await stream.CopyToAsync(output, bufferSize, length, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Operation was canceled.
                        Logger.LogMessage($"{nameof(ReadAsync)}({offset}, {length}) canceled", RemoteStorageUriById.AbsoluteUri, default);
                    }
                }
            }
        }
        

        /// <inheritdoc/>
        public async Task WriteAsync(IFileMetadata fileMetadata, Stream content = null, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(WriteAsync)}()", RemoteStorageUriById.AbsoluteUri, default, operationContext);

            if (content != null)
            {
                try
                {
                    // Update remote storage file content.
                    await Session.UploadAsync(RemoteStorageUriById, async (outputStream) =>
                    {
                        content.Position = 0; // Setting position to 0 is required in case of retry.
                        await content.CopyToAsync(outputStream);
                    }, null, content.Length, 0, -1, null, null, null, cancellationToken);
                }
                catch (Client.Exceptions.PreconditionFailedException)
                {
                    Logger.LogMessage($"Conflict. The item is modified.", RemoteStorageUriById.AbsoluteUri, default, operationContext);
                }
            }
        }
    }
}
