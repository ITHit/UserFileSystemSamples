using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common;


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
        public async Task ReadAsync(Stream output, long offset, long length, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext, CancellationToken cancellationToken)
        {
            // On Windows this method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call the resultContext.ReportProgress() or resultContext.ReturnData() method.

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ReadAsync)}({offset}, {length})", UserFileSystemPath, default, operationContext);
            
            if (!Mapping.TryGetRemoteStoragePathById(RemoteStorageItemId, out string remoteStoragePath)) return;

            cancellationToken.Register(() => { Logger.LogMessage($"{nameof(IFile)}.{nameof(ReadAsync)}({offset}, {length}) cancelled", UserFileSystemPath, default, operationContext); });

            using (FileStream stream = new FileInfo(remoteStoragePath).Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
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

            // Save ETag received from your remote storage in persistent placeholder properties.
            //string eTag = ...
            //PlaceholderItem placeholder = Engine.Placeholders.GetItem(UserFileSystemPath);
            //await placeholder.Properties.AddOrUpdateAsync("ETag", eTag);
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

            if (!Mapping.TryGetRemoteStoragePathById(RemoteStorageItemId, out string remoteStoragePath)) return;

            // Send the ETag to the server as part of the update to ensure
            // the file in the remote storge is not modified since last read.
            //string oldEtag = await placeholder.Properties["ETag"].GetValueAsync<string>();

            // Send the lock-token to the server as part of the update.
            string lockToken;
            if (Engine.Placeholders.TryGetItem(UserFileSystemPath, out PlaceholderItem placeholder))
            {
                if (placeholder.Properties.TryGetValue("LockInfo", out IDataItem propLockInfo))
                {
                    ServerLockInfo lockInfo;
                    if (propLockInfo.TryGetValue<ServerLockInfo>(out lockInfo))
                    {
                        lockToken = lockInfo.LockToken;
                    }
                }
            }

            FileInfo remoteStorageItem = new FileInfo(remoteStoragePath);

            if (content != null)
            {
                // Typically you will compare ETags on the server.
                // Here we compare ETags before the update for the demo purposes. 
                //string eTag = (await WindowsFileSystemItem.GetUsnByPathAsync(remoteStoragePath)).ToString();
                //if(oldEtag != eTag)
                //{
                //    throw new ConflictException(Modified.Server, "The file is modified in remote storage.");
                //}

                // Upload file content to the remote storage.
                using (FileStream remoteStorageStream = remoteStorageItem.Open(FileMode.Open, FileAccess.Write, FileShare.Delete))
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

            // Save ETag received from your remote storage in persistent placeholder properties.
            //string newEtag = ...
            //await placeholder.Properties.AddOrUpdateAsync("ETag", newEtag);
        }
    }
}
