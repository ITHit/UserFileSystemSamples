using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileProviderExtension.Extensions;
using ITHit.FileSystem;
using Client = ITHit.WebDAV.Client;

namespace WebDAVFileProviderExtension
{
    /// <inheritdoc cref="IFile"/>
    public class VirtualFile : VirtualFileSystemItem, IFile
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="path">File path in the user file system.</param>
        /// <param name="session">WebDAV session.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFile(string path, Client.WebDavSession session, ILogger logger) : base(path, session, logger)
        {

        }

        /// <inheritdoc/>
        public async Task OpenAsync(IOperationContext operationContext, IResultContext context)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(OpenAsync)}()", RemoteStorageID);
        }

        
        /// <inheritdoc/>
        public async Task CloseAsync(IOperationContext operationContext, IResultContext context)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(CloseAsync)}()", RemoteStorageID);
        }
     
        /// <inheritdoc/>
        public async Task ReadAsync(Stream output, long offset, long length, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext, CancellationToken cancellationToken)
        {
            // On Windows this method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer write to the output stream or call the resultContext.ReportProgress() or resultContext.ReturnData() methods.

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ReadAsync)}({offset}, {length})", RemoteStorageID, default, operationContext);

            string eTag = null;

            // Buffer size must be multiple of 4096 bytes for optimal performance.
            const int bufferSize = 0x500000; // 5Mb.
            using (Client.IWebResponse response = await Session.DownloadAsync(new Uri(RemoteStorageID), offset, length, null, cancellationToken))
            {
                using (Stream stream = await response.GetResponseStreamAsync())
                {
                    try
                    {
                        await stream.CopyToAsync(output, bufferSize, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Operation was canceled by the calling Engine.StopAsync() or the operation timeout occured.
                        Logger.LogMessage($"{nameof(ReadAsync)}({offset}, {length}) canceled", RemoteStorageID, default);
                    }
                }
                eTag = response.GetHeaderValue("ETag");
            }
        }
        

        /// <inheritdoc/>
        public async Task ValidateDataAsync(long offset, long length, IValidateDataOperationContext operationContext, IValidateDataResultContext resultContext)
        {

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ValidateDataAsync)}({offset}, {length})", RemoteStorageID);
        }

        /// <inheritdoc/>
        public async Task WriteAsync(IFileMetadata fileMetadata, Stream content = null, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(WriteAsync)}()", RemoteStorageID, default, operationContext);

            if (content != null)
            {
                try
                {
                    // Update remote storage file content.
                    string newEtag = await Session.UploadAsync(new Uri(RemoteStorageID), async (outputStream) =>
                    {
                        content.Position = 0; // Setting position to 0 is required in case of retry.
                        await content.CopyToAsync(outputStream);
                    }, null, content.Length, 0, -1, null, null, null, cancellationToken);
                }
                catch (Client.Exceptions.PreconditionFailedException)
                {
                    // Server and client ETags do not match.
                    // Set conflict status in Windows Explorer.

                    Logger.LogMessage($"Conflict. The item is modified.", RemoteStorageID, default, operationContext);
                }
            }
        }
    }
}
