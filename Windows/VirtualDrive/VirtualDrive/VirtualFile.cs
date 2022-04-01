using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;

namespace VirtualDrive
{
    /// <inheritdoc cref="IFile"/>
    public class VirtualFile : VirtualFileSystemItem, IFile
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
        public async Task OpenAsync(IOperationContext operationContext, IResultContext context)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(OpenAsync)}()", UserFileSystemPath, default, operationContext);
        }

        /// <inheritdoc/>
        public async Task CloseAsync(IOperationContext operationContext, IResultContext context)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(CloseAsync)}()", UserFileSystemPath, default, operationContext);
        }
        
        /// <inheritdoc/>
        public async Task ReadAsync(Stream output,  long offset, long length, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext)
        {
            // On Windows this method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call the resultContext.ReportProgress() or resultContext.ReturnData() method.

            Logger.LogMessage($"{nameof(IFile)}.{nameof(ReadAsync)}({offset}, {length})", UserFileSystemPath, default, operationContext);

            SimulateNetworkDelay(length, resultContext);

            string remoteStoragePath = Mapping.GetRemoteStoragePathById(RemoteStorageItemId);
            await using (FileStream stream = System.IO.File.OpenRead(remoteStoragePath))
            {
                stream.Seek(offset, SeekOrigin.Begin);
                const int bufferSize = 0x500000; // 5Mb. Buffer size must be multiple of 4096 bytes for optimal performance.
                await stream.CopyToAsync(output, bufferSize, length);
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

            //SimulateNetworkDelay(length, resultContext);

            bool isValid = true;

            resultContext.ReturnValidationResult(offset, length, isValid);
        }

        /// <inheritdoc/>
        public async Task WriteAsync(IFileMetadata fileMetadata, Stream content = null, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(WriteAsync)}()", UserFileSystemPath, default, operationContext);

            PlaceholderItem placeholder = Engine.Placeholders.GetItem(UserFileSystemPath);

            // Send the ETag to the server as part of the update to ensure
            // the file in the remote storge is not modified since last read.
            //string oldEtag = await placeholder.Properties["ETag"].GetValueAsync<string>();

            // Send the lock-token to the server as part of the update.
            string lockToken;
            IDataItem propLockInfo;
            if (placeholder.Properties.TryGetValue("LockInfo", out propLockInfo))
            {
                ServerLockInfo lockInfo;
                if (propLockInfo.TryGetValue<ServerLockInfo>(out lockInfo))
                {
                    lockToken = lockInfo.LockToken;
                }
            }

            string remoteStoragePath = Mapping.GetRemoteStoragePathById(RemoteStorageItemId);
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

            // Save ETag received from your remote storage in persistent placeholder properties.
            //string newEtag = ...
            //await placeholder.Properties.AddOrUpdateAsync("ETag", newEtag);

            //await customDataManager.SetCustomDataAsync(
            //    eTagNew,
            //    null,
            //    new[] { new FileSystemItemPropertyData((int)CustomColumnIds.ETag, eTagNew) });
        }
    }
}
