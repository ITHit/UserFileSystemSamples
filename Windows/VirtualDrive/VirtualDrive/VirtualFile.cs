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


namespace VirtualDrive
{
    /// <inheritdoc cref="IFile"/>
    public class VirtualFile : VirtualFileSystemItem, IFileWindows
    {

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="path">File path in the user file system.</param>
        /// <param name="remoteStorageItemId">Remote storage item ID.</param>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFile(string path, byte[] remoteStorageItemId, VirtualEngine engine, ILogger logger) : base(path, remoteStorageItemId, engine, logger)
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
            // To process longer requests and reset the timeout timer write to the output stream or call the resultContext.ReportProgress() or resultContext.ReturnDataAsync() methods.

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ReadAsync)}({offset}, {length})", UserFileSystemPath, default, operationContext);
            
            if (!Mapping.TryGetRemoteStoragePathById(RemoteStorageItemId, out string remoteStoragePath)) return null;

            cancellationToken.Register(() => { Logger.LogMessage($"{nameof(IFile)}.{nameof(ReadAsync)}({offset}, {length}) cancelled", UserFileSystemPath, default, operationContext); });

            using (FileStream stream = new FileInfo(remoteStoragePath).Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (output.Length != stream.Length)
                    throw new StatusException(CloudFileStatus.STATUS_CLOUD_FILE_UNSUCCESSFUL);

                stream.Seek(offset, SeekOrigin.Begin);
                const int bufferSize = 0x500000; // 5Mb. Buffer size must be multiple of 4096 bytes for optimal performance.

                try
                {
                    await stream.CopyToAsync(output, bufferSize, length, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Operation was canceled by the calling Engine.StopAsync() or the operation timeout occured.
                    Logger.LogDebug($"{nameof(ReadAsync)}({offset}, {length}) canceled", UserFileSystemPath, default, operationContext, metadata);
                }
            }

            // Return an updated item to the Engine.
            // In the returned data set the following fields:
            //  - Content eTag. The Engine will store it to determine if the file content should be updated.
            //  - Medatdata eTag. The Engine will store it to determine if the item metadata should be updated.
            return null;
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
            Logger.LogMessage($"{nameof(IFile)}.{nameof(WriteAsync)}()", UserFileSystemPath, default, operationContext);

            if (!Mapping.TryGetRemoteStoragePathById(RemoteStorageItemId, out string remoteStoragePath)) return null;

            // Send the ETag to the server as part of the update to ensure
            // the file in the remote storge is not modified since last read.
            // operationContext.Properties.TryGetETag(out string oldEtag);

            // Send the lock-token to the server as part of the update.
            // Here we read the lock-token for demo purposes only.
            operationContext.Properties.TryGetCurrentUserLockToken(out string lockToken);

            FileInfo remoteStorageItem = new FileInfo(remoteStoragePath);

            if (content != null)
            {
                // Upload file content to the remote storage.
                using (FileStream remoteStorageStream = remoteStorageItem.Open(FileMode.Open, FileAccess.Write, FileShare.Delete))
                {
                    await content.CopyToAsync(remoteStorageStream, cancellationToken);
                    remoteStorageStream.SetLength(content.Length);
                }
            }

            // Update remote storage file metadata.
            if (metadata.Attributes.HasValue)
            {
                remoteStorageItem.Attributes = metadata.Attributes.Value & ~FileAttributes.ReadOnly;
            }

            if (metadata.CreationTime.HasValue)
            {
                remoteStorageItem.CreationTimeUtc = metadata.CreationTime.Value.UtcDateTime;
            }

            if (metadata.LastWriteTime.HasValue)
            {
                remoteStorageItem.LastWriteTimeUtc = metadata.LastWriteTime.Value.UtcDateTime;
            }

            if (metadata.LastAccessTime.HasValue)
            {
                remoteStorageItem.LastAccessTimeUtc = metadata.LastAccessTime.Value.UtcDateTime;
            }

            if (metadata.Attributes.HasValue)
            {
                remoteStorageItem.Attributes = metadata.Attributes.Value;
            }

            // Return an updated item to the Engine.
            // In the returned data set the following fields:
            //  - Content eTag. The Engine will store it to determine if the file content should be updated.
            //  - Medatdata eTag. The Engine will store it to determine if the item metadata should be updated.
            return null;
        }
    }
}
