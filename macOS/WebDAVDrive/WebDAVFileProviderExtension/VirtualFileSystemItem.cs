using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;
using ITHit.WebDAV.Client;
using WebDAVCommon;
using Client = ITHit.WebDAV.Client;

namespace WebDAVFileProviderExtension
{
    public abstract class VirtualFileSystemItem : IFileSystemItemMac, ILock
    {
        /// <summary>
        /// ID on the remote storage.
        /// </summary>
        protected readonly byte[] RemoteStorageId;

        /// <summary>
        /// Uri on the remote storage.
        /// </summary>
        protected readonly Uri RemoteStorageUriById;

        /// <summary>
        /// Logger.
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Engine instance.
        /// </summary>
        protected VirtualEngine Engine;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="remoteStorageId">Id uri on the WebDav server.</param>
        /// <param name="engine">Engine.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFileSystemItem(byte[] remoteStorageId, VirtualEngine engine, ILogger logger)
        {
            if (remoteStorageId == null)
            {
                throw new ArgumentNullException(nameof(remoteStorageId));
            }

            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            RemoteStorageId = remoteStorageId;
            RemoteStorageUriById = Mapping.GetUriById(remoteStorageId, engine.WebDAVServerUrl);

            Engine = engine;
        }


        ///<inheritdoc>
        public async Task MoveToAsync(string targetUserFileSystemPath, byte[] targetFolderRemoteStorageItemId, IOperationContext operationContext = null, IConfirmationResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToAsync)}()", RemoteStorageUriById.AbsoluteUri, targetUserFileSystemPath);
            Uri targetFolderUri = await new VirtualFolder(targetFolderRemoteStorageItemId, Engine, Logger).GetItemHrefAsync();
            string targetItemName = targetUserFileSystemPath.Split(Path.DirectorySeparatorChar).Last();
            Uri targetItemUri = new Uri(targetFolderUri, targetItemName);

            await Engine.WebDavSession.MoveToAsync(await GetItemHrefAsync(), targetItemUri, true);
            Logger.LogMessage("Moved item in remote storage succesefully", RemoteStorageUriById.AbsoluteUri, targetItemUri.AbsoluteUri);
        }

        ///<inheritdoc>
        public async Task DeleteAsync(IOperationContext operationContext = null, IConfirmationResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteAsync)}()", RemoteStorageUriById.AbsoluteUri);

            await Engine.WebDavSession.DeleteAsync(RemoteStorageUriById);
        }

        ///<inheritdoc>
        public async Task<IFileSystemItemMetadata?> GetMetadataAsync(IResultContext resultContext = null)
        {
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(GetMetadataAsync)}()", RemoteStorageUriById.AbsoluteUri);

            IHierarchyItem? item = null;
            Client.PropertyName[] propNames = new Client.PropertyName[2];
            propNames[0] = new Client.PropertyName("resource-id", "DAV:");
            propNames[1] = new Client.PropertyName("parent-resource-id", "DAV:");

            try
            {
                // Return IFileMetadata for a file, IFolderMetadata for a folder.
                item = (await Engine.WebDavSession.GetItemAsync(RemoteStorageUriById, propNames)).WebDavResponse;
            }
            catch (Client.Exceptions.NotFoundException e)
            {
                Logger.LogError($"{nameof(IFileSystemItem)}.{nameof(GetMetadataAsync)}()", RemoteStorageUriById.AbsoluteUri, ex: e);

                item = null;
            }
            catch (Client.Exceptions.WebDavHttpException httpException)
            {
                HandleWebExceptions(httpException, resultContext);                
            }

            return item != null ? Mapping.GetUserFileSystemItemMetadata(item) : null;
        }

        /// <summary>
        /// Returns Uri of item.
        /// </summary>
        public async Task<Uri> GetItemHrefAsync()
        {
            return (await Engine.WebDavSession.GetItemAsync(RemoteStorageUriById, propNames: null)).WebDavResponse.Href;
        }


        ///<inheritdoc>
        public async Task<byte[]?> GetThumbnailAsync(uint size, IOperationContext operationContext)
        {
            byte[]? thumbnail = null;

            string[] exts = AppGroupSettings.Settings.Value.RequestThumbnailsFor.Trim().Split("|");
            Uri itemUri = await GetItemHrefAsync();
            string ext = System.IO.Path.GetExtension(itemUri.AbsoluteUri).TrimStart('.');

            if (exts.Any(ext.Equals) || exts.Any("*".Equals))
            {
                string ThumbnailGeneratorUrl = AppGroupSettings.Settings.Value.ThumbnailGeneratorUrl
                    .Replace("{thumbnail width}", size.ToString())
                    .Replace("{thumbnail height}", size.ToString());
                string filePathRemote = ThumbnailGeneratorUrl.Replace("{path to file}", itemUri.AbsoluteUri);

                try
                {
                    using (IDownloadResponse response = await Engine.WebDavSession.DownloadAsync(new Uri(filePathRemote)))
                    {
                        using (Stream stream = await response.GetResponseStreamAsync())
                        {
                            thumbnail = await StreamToByteArrayAsync(stream);
                        }
                    }
                }
                catch (System.Net.WebException we)
                {
                    Logger.LogMessage(we.Message, RemoteStorageUriById.AbsoluteUri);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Failed to load thumbnail {size}px", RemoteStorageUriById.AbsoluteUri, null, e);
                }
            }

            string thumbnailResult = thumbnail != null ? "Success" : "Not Impl";
            Logger.LogMessage($"{nameof(VirtualEngine)}.{nameof(GetThumbnailAsync)}() - {thumbnailResult}", RemoteStorageUriById.AbsoluteUri);

            return thumbnail;
        }

        private async Task<byte[]> StreamToByteArrayAsync(Stream stream)
        {
            using (MemoryStream memoryStream = new())
            {
                await stream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }

        ///<inheritdoc>
        public Task<IEnumerable<FileSystemItemPropertyData>> GetPropertiesAsync(IOperationContext operationContext)
        {
            throw new NotImplementedException();
        }

        protected async void HandleWebExceptions(Client.Exceptions.WebDavHttpException webDavHttpException, IResultContext resultContext)
        {
            switch (webDavHttpException.Status.Code)
            {
                // Challenge-responce auth: Basic, Digest, NTLM or Kerberos
                case 401:
                    
                    if (Engine.WebDavSession.Credentials == null || !(Engine.WebDavSession.Credentials is NetworkCredential) ||
                        (Engine.WebDavSession.Credentials as NetworkCredential).UserName != await Engine.SecureStorage.GetAsync("UserName"))
                    {
                        // Set login type to display sing in button in Finder.
                        await Engine.SecureStorage.RequireAuthenticationAsync();
                        if (resultContext != null)
                        {
                            resultContext.ReportStatus(CloudFileStatus.STATUS_CLOUD_FILE_AUTHENTICATION_FAILED);
                        }
                    }
                    else
                    {
                        // Reset WebDavSession.
                        Engine.InitWebDavSession();
                    }
                    break;
            }    
        }

        public async Task LockAsync(LockMode lockMode, IOperationContext operationContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(ILock)}.{nameof(LockAsync)}()", RemoteStorageUriById.AbsoluteUri, default, operationContext);

            // Call your remote storage here to lock the item.
            // Supply the lock-token as part of each remote storage update in IFile.WriteAsync() method.
            // Note that the actual lock timout applied by the server may be different from the one requested.

            string lockOwner = Engine.CurrentUserPrincipal;
            double timOutMs = lockMode == LockMode.Auto ? Engine.AutoLockTimoutMs : Engine.ManualLockTimoutMs;
            TimeSpan timeOut = timOutMs == -1 ? TimeSpan.MaxValue : TimeSpan.FromMilliseconds(timOutMs);

            LockInfo lockInfo = (await Engine.WebDavSession.LockAsync(Mapping.GetUriById(RemoteStorageId, Engine.WebDAVServerUrl), LockScope.Exclusive, false, lockOwner, timeOut, null, cancellationToken)).WebDavResponse;

            // Save lock-token and lock-mode. Start the timer to refresh the lock.
            await SaveLockAsync(lockInfo, lockMode, cancellationToken);
        }

        /// <summary>
        /// Starts a timer to extend the lock.
        /// </summary>
        /// <param name="lockInfo">Lock info.</param>
        /// <param name="lockMode">Lock mode.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task object that can be awaited.</returns>
        private async Task SaveLockAsync(LockInfo lockInfo, LockMode lockMode, CancellationToken cancellationToken)
        {
            Logger.LogDebug($"Locked/Refreshed by {lockInfo.Owner}, timout: {lockInfo.TimeOut:hh\\:mm\\:ss\\.ff}", RemoteStorageUriById.AbsoluteUri);

            // Start the timer to extend (refresh) the automatic lock when it is about to expire.
            if (lockInfo.TimeOut < TimeSpan.MaxValue && lockMode == LockMode.Auto)
            {
                // We want to refresh the lock some time before the lock expires.
                // Either 1 minute before, for release config, or 1/5 of a timout time, for dev config.
                double refreshTimeOut = lockInfo.TimeOut.TotalMilliseconds;
                refreshTimeOut -= refreshTimeOut > 120000 ? 60000 : refreshTimeOut / 5;

                var timer = new System.Timers.Timer(refreshTimeOut);
                timer.AutoReset = false;
                timer.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) =>
                {
                    LockRefreshAsync(
                        lockInfo.LockToken.LockToken,
                        lockInfo.TimeOut,
                        lockMode,
                        cancellationToken,
                        timer);
                };
                timer.Start();
            }
        }

        private async void LockRefreshAsync(string lockToken, TimeSpan timOut, LockMode lockMode, CancellationToken cancellationToken, System.Timers.Timer timer)
        {
            try
            {
                timer.Dispose();

                if (cancellationToken.IsCancellationRequested) return;

                // Extend (refresh) the lock.                
                IHierarchyItem item = (await Engine.WebDavSession.GetItemAsync(RemoteStorageUriById, null, cancellationToken)).WebDavResponse;
                LockInfo lockInfo = (await item.RefreshLockAsync(lockToken, timOut, null, cancellationToken)).WebDavResponse;

                Logger.LogMessage($"Lock extended, new timout: {lockInfo.TimeOut:hh\\:mm\\:ss\\.ff}", RemoteStorageUriById.AbsoluteUri);

                // Save the new lock. Start the timer to refresh the lock.
                await SaveLockAsync(lockInfo, lockMode, cancellationToken);

            }
            catch (TaskCanceledException)
            {
                Logger.LogDebug("Lock refresh canceled", RemoteStorageUriById.AbsoluteUri);
            }
            catch (Exception ex)
            {
                Logger.LogError("Lock refresh failed", RemoteStorageUriById.AbsoluteUri, default, ex);
            }
        }

        public async Task<LockMode> GetLockModeAsync(IOperationContext operationContext = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task UnlockAsync(IOperationContext operationContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(ILock)}.{nameof(UnlockAsync)}()", RemoteStorageUriById.AbsoluteUri, default, operationContext);

            IFileSystemItemMetadata fileSystemItemMetadata = await GetMetadataAsync();

            if (fileSystemItemMetadata != null && fileSystemItemMetadata.Properties.TryGetValue("LockToken", out IDataItem lockInfoData))
            {
                if (lockInfoData.TryGetValue<ServerLockInfo>(out ServerLockInfo lockInfo) && lockInfo.Owner == Engine.CurrentUserPrincipal)
                {
                    LockUriTokenPair[] lockTokens = new LockUriTokenPair[] { new LockUriTokenPair(RemoteStorageUriById, lockInfo.LockToken) };

                    // Unlock the item in the remote storage.
                    try
                    {
                        await Engine.WebDavSession.UnlockAsync(RemoteStorageUriById, lockTokens, null, cancellationToken);
                        Logger.LogDebug("Unlocked in the remote storage successfully", RemoteStorageUriById.AbsoluteUri, default, operationContext);
                    }
                    catch (ITHit.WebDAV.Client.Exceptions.ConflictException)
                    {
                        Logger.LogDebug("The item is already unlocked.", RemoteStorageUriById.AbsoluteUri, default, operationContext);
                    }
                }
            }
        }
    }
}
