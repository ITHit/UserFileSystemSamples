using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common;


namespace VirtualFileSystem
{
    /// <inheritdoc cref="IFile"/>
    public class VirtualFile : VirtualFileSystemItem, IFileWindows
    {

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="mapping">Maps a the remote storage path and data to the user file system path and data.</param>
        /// <param name="path">File path in the user file system.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFile(IMapping mapping, string path, ILogger logger) : base(mapping, path, logger)
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

            using (FileStream stream = new FileInfo(RemoteStoragePath).Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                stream.Seek(offset, SeekOrigin.Begin);
                const int bufferSize = 0x500000; // 5Mb. Buffer size must be multiple of 4096 bytes for optimal performance.
                try
                {
                    await stream.CopyToAsync(output, bufferSize, length, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Operation was canceled by the calling Engine.StopAsync() or the operation timeout occured.
                    Logger.LogDebug($"{nameof(ReadAsync)}({offset}, {length}) canceled", UserFileSystemPath, default);
                }
            }
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

            FileInfo remoteStorageItem = new FileInfo(RemoteStoragePath);

            if (content != null)
            {
                // Upload remote storage file content.
                using (FileStream remoteStorageStream = remoteStorageItem.Open(FileMode.Open, FileAccess.Write, FileShare.Delete))
                {
                    await content.CopyToAsync(remoteStorageStream, cancellationToken);
                    remoteStorageStream.SetLength(content.Length);
                }
            }

            // Update remote storage file metadata.
            if (fileBasicInfo.Attributes.HasValue)
            {
                remoteStorageItem.Attributes = fileBasicInfo.Attributes.Value & ~FileAttributes.ReadOnly;
            }

            if (fileBasicInfo.CreationTime.HasValue)
            {
                remoteStorageItem.CreationTimeUtc = fileBasicInfo.CreationTime.Value.UtcDateTime;
            }

            if (fileBasicInfo.LastWriteTime.HasValue)
            {
                remoteStorageItem.LastWriteTimeUtc = fileBasicInfo.LastWriteTime.Value.UtcDateTime;
            }

            if (fileBasicInfo.LastAccessTime.HasValue)
            {
                remoteStorageItem.LastAccessTimeUtc = fileBasicInfo.LastAccessTime.Value.UtcDateTime;
            }

            if (fileBasicInfo.Attributes.HasValue)
            {
                remoteStorageItem.Attributes = fileBasicInfo.Attributes.Value;
            }

            // On macOS you must return updated file info.
            // On Windows you can return null.
            return null;
        }
    }
}
