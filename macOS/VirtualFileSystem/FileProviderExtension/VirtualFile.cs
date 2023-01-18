using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileProviderExtension.Extensions;
using ITHit.FileSystem;

namespace FileProviderExtension
{
    /// <inheritdoc cref="IFile"/>
    public class VirtualFile : VirtualFileSystemItem, IFile
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="path">File path in the user file system.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFile(string path, ILogger logger) : base(path, logger)
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
        public async Task ReadAsync(Stream output, long offset, long length, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext, CancellationToken cancellationToken)
        {

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

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ValidateDataAsync)}({offset}, {length})", UserFileSystemPath);
        }

        /// <inheritdoc/>
        public async Task WriteAsync(IFileMetadata fileMetadata, Stream content = null, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(WriteAsync)}()", UserFileSystemPath);

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
        }
    }
}
