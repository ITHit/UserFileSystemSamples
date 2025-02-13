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
        /// <param name="remoteStoragePath">File or folder path in the remote system.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFile(string remoteStoragePath, ILogger logger) : base(remoteStoragePath, logger)
        {

        }        
     
        /// <inheritdoc/>
        public async Task<IFileMetadata> ReadAsync(Stream output, long offset, long length, IFileMetadata metadata, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext, CancellationToken cancellationToken)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(ReadAsync)}({offset}, {length})", RemoteStoragePath);

            await using (FileStream stream = System.IO.File.OpenRead(RemoteStoragePath))
            {
                stream.Seek(offset, SeekOrigin.Begin);
                const int bufferSize = 0x500000; // 5Mb. Buffer size must be multiple of 4096 bytes for optimal performance.
                await stream.CopyToAsync(output, bufferSize, length);
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<IFileMetadata> WriteAsync(IFileSystemBasicInfo fileBasicInfo, Stream content = null, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(WriteAsync)}()", RemoteStoragePath);

            FileInfo remoteStorageItem = new FileInfo(RemoteStoragePath);

            if (content != null)
            {
                // Upload remote storage file content.
                await using (FileStream remoteStorageStream = remoteStorageItem.Open(FileMode.Open, FileAccess.Write, FileShare.Delete))
                {
                    await content.CopyToAsync(remoteStorageStream);
                    remoteStorageStream.SetLength(content.Length);
                }

                if (fileBasicInfo.LastWriteTime != null)
                {
                    remoteStorageItem.LastWriteTimeUtc = fileBasicInfo.LastWriteTime.Value.UtcDateTime;                    
                }
            }

            // Update remote storage file metadata.
            if (fileBasicInfo.Attributes != null)
            {
                remoteStorageItem.Attributes = fileBasicInfo.Attributes.Value;
            }

            if (fileBasicInfo.CreationTime != null)
            {
                remoteStorageItem.CreationTimeUtc = fileBasicInfo.CreationTime.Value.UtcDateTime;
            }        

            if (fileBasicInfo.LastAccessTime != null)
            {
                remoteStorageItem.LastAccessTimeUtc = fileBasicInfo.LastAccessTime.Value.UtcDateTime;
            }

            return await GetMetadataAsync() as IFileMetadata;
        }
    }
}
