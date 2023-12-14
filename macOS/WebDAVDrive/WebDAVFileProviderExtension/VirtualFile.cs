using FileProviderExtension.Extensions;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;
using WebDAVCommon;
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
        /// <param name="engine">Engine.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFile(byte[] remoteStorageId, VirtualEngine engine, ILogger logger) : base(remoteStorageId, engine, logger)
        {

        }
     
        /// <inheritdoc/>
        public async Task ReadAsync(Stream output, long offset, long length, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext, CancellationToken cancellationToken)
        {           
            Logger.LogMessage($"{nameof(IFile)}.{nameof(ReadAsync)}({offset}, {length})", RemoteStorageUriById.AbsoluteUri, default, operationContext);

            // Buffer size must be multiple of 4096 bytes for optimal performance.
            const int bufferSize = 0x500000; // 5Mb.
            using (Client.IDownloadResponse response = await Engine.WebDavSession.DownloadAsync(new Uri(RemoteStorageUriById.AbsoluteUri), offset, length, null, cancellationToken))
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
                    catch (Client.Exceptions.WebDavHttpException httpException)
                    {
                        HandleWebExceptions(httpException, resultContext);
                    }
                }
            }
        }
        

        /// <inheritdoc/>
        public async Task<IFileMetadata> WriteAsync(IFileSystemBasicInfo fileBasicInfo, Stream content = null, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFile)}.{nameof(WriteAsync)}()", RemoteStorageUriById.AbsoluteUri, default, operationContext);

            if (content != null)
            {
                string? eTag = null;
                if (operationContext.Properties.TryGetValue("eTag", out IDataItem eTagData))
                {
                    eTagData.TryGetValue<string>(out eTag);
                }

                Client.LockUriTokenPair[] lockTokens = null;
                if (operationContext.Properties.TryGetValue("LockToken", out IDataItem lockInfoData))
                {                   
                    if (lockInfoData.TryGetValue<ServerLockInfo>(out ServerLockInfo lockInfo) && lockInfo.Owner == Engine.CurrentUserPrincipal)
                    {                       
                        lockTokens = new Client.LockUriTokenPair[] { new Client.LockUriTokenPair(RemoteStorageUriById, lockInfo.LockToken) };
                    }
                }

                try
                {
                    // Update remote storage file content.
                    await Engine.WebDavSession.UploadAsync(RemoteStorageUriById, async (outputStream) =>
                    {
                        content.Position = 0; // Setting position to 0 is required in case of retry.
                        await content.CopyToAsync(outputStream);
                    }, null, content.Length, 0, -1, lockTokens, eTag, null, cancellationToken);


                    // macOS requires last modification date on the server to match the client. If date does not match, the file will be redownloaded.
                    // Here we use property name indetical to Microsoft Windows Explorer for max interability.
                    if (fileBasicInfo.LastWriteTime.HasValue)
                    {
                        Client.IFile file = (await Engine.WebDavSession.GetFileAsync(RemoteStorageUriById.AbsoluteUri, null, cancellationToken)).WebDavResponse;
                        Client.Property[] propsToAddAndUpdate = new Client.Property[1];
                        propsToAddAndUpdate[0] = new Client.Property(new Client.PropertyName("Win32LastModifiedTime", "urn:schemas-microsoft-com:"), fileBasicInfo.LastWriteTime.ToString());

                        await file.UpdatePropertiesAsync(propsToAddAndUpdate, null, lockTokens?.FirstOrDefault()?.LockToken);
                    }
                }
                catch (Client.Exceptions.PreconditionFailedException)
                {
                    Logger.LogMessage($"Conflict. The item is modified.", RemoteStorageUriById.AbsoluteUri, default, operationContext);
                }           
            }

            return await GetMetadataAsync() as IFileMetadata;
        }
    }
}
