using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.WebDAV.Client;
using ITHit.WebDAV.Client.Exceptions;


namespace WebDAVDrive
{
    ///<inheritdoc>
    public abstract class VirtualFileSystemItem : IFileSystemItemWindows, ILock
    {
        /// <summary>
        /// File or folder path in the user file system.
        /// </summary>
        protected readonly string UserFileSystemPath;

        /// <summary>
        /// File or folder item ID in the remote storage.
        /// </summary>
        protected readonly byte[] RemoteStorageItemId;

        /// <summary>
        /// Path of this file or folder in the remote storage.
        /// </summary>
        protected readonly string RemoteStoragePath;

        /// <summary>
        /// Logger.
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Engine.
        /// </summary>
        protected readonly VirtualEngine Engine;

        /// <summary>
        /// Automatic lock timout in milliseconds.
        /// </summary>
        private readonly double autoLockTimoutMs;

        /// <summary>
        /// Manual lock timout in milliseconds.
        /// </summary>
        private readonly double manualLockTimoutMs;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="remoteStorageId">Remote storage item ID.</param>
        /// <param name="userFileSystemPath">User file system path. This paramater is available on Windows platform only. On macOS and iOS this parameter is always null.</param>
        /// <param name="engine">Engine instance.</param>
        /// <param name="autoLockTimoutMs">Automatic lock timout in milliseconds.</param>
        /// <param name="manualLockTimoutMs">Manual lock timout in milliseconds.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFileSystemItem(byte[] remoteStorageId, string userFileSystemPath, VirtualEngine engine, double autoLockTimoutMs, double manualLockTimoutMs, ILogger logger)
        {
            if (string.IsNullOrEmpty(userFileSystemPath))
            {
                throw new ArgumentNullException(nameof(userFileSystemPath));
            }
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Engine = engine ?? throw new ArgumentNullException(nameof(engine));

            UserFileSystemPath = userFileSystemPath;
            RemoteStorageItemId = remoteStorageId;
            RemoteStoragePath = Mapping.MapPath(userFileSystemPath);

            this.autoLockTimoutMs = autoLockTimoutMs;
            this.manualLockTimoutMs = manualLockTimoutMs;
        }

        ///<inheritdoc>
        public async Task MoveToAsync(string targetUserFileSystemPath, byte[] targetParentItemId, IOperationContext operationContext = null, IConfirmationResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewPath = targetUserFileSystemPath;
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogDebug($"{nameof(IFileSystemItem)}.{nameof(MoveToAsync)}()", userFileSystemOldPath, userFileSystemNewPath, operationContext);
        }

        /// <inheritdoc/>
        public async Task MoveToCompletionAsync(string targetUserFileSystemPath, byte[] targetFolderRemoteStorageItemId, IMoveCompletionContext operationContext = null, IInSyncStatusResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewPath = targetUserFileSystemPath;
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToCompletionAsync)}()", userFileSystemOldPath, userFileSystemNewPath, operationContext);

            string remoteStorageOldPath = RemoteStoragePath;
            string remoteStorageNewPath = Mapping.MapPath(userFileSystemNewPath);

            await Program.DavClient.MoveToAsync(new Uri(remoteStorageOldPath), new Uri(remoteStorageNewPath), true, null, null, cancellationToken);
            Logger.LogDebug("Moved in the remote storage successfully", userFileSystemOldPath, targetUserFileSystemPath, operationContext);
        }

        ///<inheritdoc>
        public async Task DeleteAsync(IOperationContext operationContext, IConfirmationResultContext resultContext, CancellationToken cancellationToken = default)
        {
            Logger.LogDebug($"{nameof(IFileSystemItem)}.{nameof(DeleteAsync)}()", UserFileSystemPath, default, operationContext);

            // To cancel the operation and prevent the file from being deleted, 
            // call the resultContext.ReturnErrorResult() method or throw any exception inside this method.

            // IMPOTRTANT! See Windows Cloud API delete prevention bug description here: 
            // https://stackoverflow.com/questions/68887190/delete-in-cloud-files-api-stopped-working-on-windows-21h1
            // https://docs.microsoft.com/en-us/answers/questions/75240/bug-report-cfapi-ackdelete-borken-on-win10-2004.html

            // Note that some applications, such as Windows Explorer may call delete more than one time on the same file/folder.
        }

        /// <inheritdoc/>
        public async Task DeleteCompletionAsync(IOperationContext operationContext, IInSyncStatusResultContext resultContext, CancellationToken cancellationToken = default)
        {
            // On Windows, for move with overwrite on folders to function correctly, 
            // the deletion of the folder in the remote storage must be done in DeleteCompletionAsync()
            // Otherwise the folder will be deleted before files in it can be moved.

            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteCompletionAsync)}()", UserFileSystemPath, default, operationContext);

            try
            {
                await Program.DavClient.DeleteAsync(new Uri(RemoteStoragePath), null, null, cancellationToken);
                Logger.LogDebug("Deleted in the remote storage successfully", UserFileSystemPath, default, operationContext);
            }
            catch(NotFoundException ex)
            {
                // The item is not found. We do not want the Engine to repeat the delete operation.
                Logger.LogDebug("Item already deleted", UserFileSystemPath, default, operationContext);
            }
            catch (WebDavHttpException ex)
            {
                // We want the Engine to try deleting this item again later.
                resultContext.SetInSync = false;
                Logger.LogMessage(ex.Message, UserFileSystemPath, default, operationContext);
            }
        }

        
        public async Task<byte[]> GetThumbnailAsync(uint size, IOperationContext operationContext = null)
        {
            byte[] thumbnail = null;

            string[] exts = Program.Settings.RequestThumbnailsFor.Trim().Split("|");
            string ext = System.IO.Path.GetExtension(UserFileSystemPath).TrimStart('.');

            if (exts.Any(ext.Equals) || exts.Any("*".Equals))
            {
                string ThumbnailGeneratorUrl = Program.Settings.ThumbnailGeneratorUrl.Replace("{thumbnail width}", size.ToString()).Replace("{thumbnail height}", size.ToString());
                string filePathRemote = ThumbnailGeneratorUrl.Replace("{path to file}", WebDAVDrive.Mapping.MapPath(UserFileSystemPath));

                try
                {
                    using (IDownloadResponse response = await Program.DavClient.DownloadAsync(new Uri(filePathRemote)))
                    {
                        using (Stream stream = await response.GetResponseStreamAsync())
                        {
                            thumbnail = await StreamToByteArrayAsync(stream);
                        }
                    }
                }
                catch (System.Net.WebException we)
                {
                    Logger.LogMessage(we.Message, UserFileSystemPath);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Failed to load thumbnail {size}px", UserFileSystemPath, null, e);
                }
            }

            string thumbnailResult = thumbnail != null ? "Success" : "Not Impl";
            Logger.LogMessage($"{nameof(VirtualEngine)}.{nameof(GetThumbnailAsync)}() - {thumbnailResult}", UserFileSystemPath);

            return thumbnail;
        }

        private static async Task<byte[]> StreamToByteArrayAsync(Stream stream)
        {
            using (MemoryStream memoryStream = new())
            {
                await stream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }
        

        /// <inheritdoc/>
        public async Task<IEnumerable<FileSystemItemPropertyData>> GetPropertiesAsync(IOperationContext operationContext = null)
        {
            // For this method to be called you need to register a properties handler.
            // See method description for more details.

            Logger.LogDebug($"{nameof(IFileSystemItem)}.{nameof(GetPropertiesAsync)}()", UserFileSystemPath);

            IList<FileSystemItemPropertyData> props = new List<FileSystemItemPropertyData>();

            if (Engine.Placeholders.TryGetItem(UserFileSystemPath, out PlaceholderItem placeholder))
            {

                // Read LockInfo and choose the lock icon.
                if (placeholder.TryGetLockInfo(out ServerLockInfo lockInfo))
                {
                    // Determine if the item is locked by this user or thirt-party user.
                    bool thisUser = Engine.CurrentUserPrincipal.Equals(lockInfo.Owner, StringComparison.InvariantCultureIgnoreCase);
                    string lockIconName = thisUser ? "Locked" : "LockedByAnotherUser";

                    // Get Lock Mode.
                    if(thisUser && (lockInfo.Mode == LockMode.Auto))
                    {
                        lockIconName += "Auto";
                    }

                    // Set Lock Owner.
                    FileSystemItemPropertyData propertyLockOwner = new FileSystemItemPropertyData()
                    {
                        Id = (int)CustomColumnIds.LockOwnerIcon,
                        Value = lockInfo.Owner,
                        IconResource = Path.Combine(Engine.IconsFolderPath, lockIconName + ".ico")
                    };
                    props.Add(propertyLockOwner);

                    // Set Lock Expires.
                    FileSystemItemPropertyData propertyLockExpires = new FileSystemItemPropertyData()
                    {
                        Id = (int)CustomColumnIds.LockExpirationDate,
                        Value = lockInfo.LockExpirationDateUtc.ToString(),
                        IconResource = Path.Combine(Engine.IconsFolderPath, "Empty.ico")
                    };
                    props.Add(propertyLockExpires);

                    // Set Lock Scope
                    FileSystemItemPropertyData propertyLockScope = new FileSystemItemPropertyData()
                    {
                        Id = (int)CustomColumnIds.LockScope,
                        Value = lockInfo.Exclusive ? "Exclusive" : "Shared",
                        IconResource = Path.Combine(Engine.IconsFolderPath, "Empty.ico")
                    };
                    props.Add(propertyLockScope);
                }


                // Read ETag.
                if (placeholder.TryGetETag(out string eTag))
                {
                    FileSystemItemPropertyData propertyETag = new FileSystemItemPropertyData()
                    {
                        Id = (int)CustomColumnIds.ETag,
                        Value = eTag,
                        IconResource = Path.Combine(Engine.IconsFolderPath, "Empty.ico")
                    };
                    props.Add(propertyETag);
                }
            }

            return props;
        }   

        
        ///<inheritdoc>
        public async Task LockAsync(LockMode lockMode, IOperationContext operationContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(ILock)}.{nameof(LockAsync)}()", UserFileSystemPath, default, operationContext);

            // Call your remote storage here to lock the item.
            // Save the lock token and other lock info received from the remote storage on the client.
            // Supply the lock-token as part of each remote storage update in IFile.WriteAsync() method.
            // Note that the actual lock timout applied by the server may be different from the one requested.

            string lockOwner = Engine.CurrentUserPrincipal;
            double timOutMs = lockMode == LockMode.Auto ? autoLockTimoutMs : manualLockTimoutMs;
            TimeSpan timeOut = timOutMs == -1 ? TimeSpan.MaxValue : TimeSpan.FromMilliseconds(timOutMs);

            LockInfo lockInfo = (await Program.DavClient.LockAsync(new Uri(RemoteStoragePath), LockScope.Exclusive, false, lockOwner, timeOut, null, cancellationToken)).WebDavResponse;

            // Save lock-token and lock-mode. Start the timer to refresh the lock.
            await SaveLockAsync(lockInfo, lockMode, cancellationToken);
        }

        /// <summary>
        /// Saves lock token, lock mode, refreshes Windows Explorer UI and starts a timer to extend the lock.
        /// </summary>
        /// <param name="lockInfo">Lock info.</param>
        /// <param name="lockMode">Lock mode.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task object that can be awaited.</returns>
        private async Task SaveLockAsync(LockInfo lockInfo, LockMode lockMode, CancellationToken cancellationToken)
        {
            if (Engine.Placeholders.TryGetItem(UserFileSystemPath, out PlaceholderItem placeholder))
            {
                // Save lock-token and lock-mode.
                ServerLockInfo serverLockInfo = new ServerLockInfo
                {
                    LockToken = lockInfo.LockToken.LockToken,
                    Exclusive = lockInfo.LockScope == LockScope.Exclusive,
                    Owner = lockInfo.Owner,
                    LockExpirationDateUtc = DateTimeOffset.Now.Add(lockInfo.TimeOut),
                    Mode = lockMode
                };
                placeholder.SetLockInfo(serverLockInfo);
                placeholder.UpdateUI();

                Logger.LogDebug($"Locked/Refreshed by {lockInfo.Owner}, timout: {lockInfo.TimeOut:hh\\:mm\\:ss\\.ff}", UserFileSystemPath);

                // Start the timer to extend (refresh) the automatic lock when it is about to expire.
                if (lockInfo.TimeOut < TimeSpan.MaxValue && lockMode == LockMode.Auto)
                {
                    // We want to refresh the lock some time before the lock expires.
                    // Either 1 minute before, for release config, or 1/5 of a timout time, for dev config.
                    double refreshTimeOut = lockInfo.TimeOut.TotalMilliseconds;
                    refreshTimeOut -= refreshTimeOut > 120000 ? 60000 : refreshTimeOut / 5;

                    var timer = new System.Timers.Timer(refreshTimeOut);
                    timer.AutoReset = false;
                    timer.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) => { 
                        LockRefreshAsync(
                            lockInfo.LockToken.LockToken, 
                            lockInfo.TimeOut, 
                            lockMode, 
                            cancellationToken, 
                            timer); };
                    timer.Start();
                }
            }
        }

        private async void LockRefreshAsync(string lockToken, TimeSpan timOut, LockMode lockMode, CancellationToken cancellationToken, System.Timers.Timer timer)
        {
            try
            {
                timer.Dispose();

                if (cancellationToken.IsCancellationRequested) return;

                if (Engine.Placeholders.TryGetItem(UserFileSystemPath, out PlaceholderItem placeholder))
                {
                    // Check that the item is still locked.
                    if (placeholder.TryGetLockInfo(out ServerLockInfo serverLockInfo))
                    {
                        // The item may be unlocked and than locked again. Check that the stored lock is the same as passed to this method.
                        bool sameToken = lockToken.Equals(serverLockInfo.LockToken, StringComparison.InvariantCultureIgnoreCase);
                        if (sameToken)
                        {
                            // Extend (refresh) the lock.
                            //Program.DavClient.RefreshLockAsync(new Uri(RemoteStoragePath), lockToken, timout, cancellationToken);
                            IHierarchyItem item = (await Program.DavClient.GetItemAsync(new Uri(RemoteStoragePath), null, cancellationToken)).WebDavResponse;
                            LockInfo lockInfo = (await item.RefreshLockAsync(lockToken, timOut, null, cancellationToken)).WebDavResponse;

                            Logger.LogMessage($"Lock extended, new timout: {lockInfo.TimeOut:hh\\:mm\\:ss\\.ff}", UserFileSystemPath);

                            // Save the new lock. Start the timer to refresh the lock.
                            await SaveLockAsync(lockInfo, lockMode, cancellationToken);
                        }
                    }
                }
            }
            catch(TaskCanceledException ex)
            {
                Logger.LogDebug("Lock refresh canceled", UserFileSystemPath);
            }
            catch (Exception ex)
            {
                Logger.LogError("Lock refresh failed", UserFileSystemPath, default, ex);
            }
        }
        

        
        ///<inheritdoc>
        public async Task<LockMode> GetLockModeAsync(IOperationContext operationContext = null, CancellationToken cancellationToken = default)
        {
            if (Engine.Placeholders.TryGetItem(UserFileSystemPath, out PlaceholderItem placeholder))
            {
                if (placeholder.TryGetLockInfo(out ServerLockInfo lockInfo))
                {
                    return lockInfo.Mode;
                }
            }

            return LockMode.None;
        }
        

        
        ///<inheritdoc>
        public async Task UnlockAsync(IOperationContext operationContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(ILock)}.{nameof(UnlockAsync)}()", UserFileSystemPath, default, operationContext);

            if (Engine.Placeholders.TryGetItem(UserFileSystemPath, out PlaceholderItem placeholder))
            {
                // Read the lock-token.
                if (placeholder.TryGetLockInfo(out ServerLockInfo lockInfo))
                {
                    LockUriTokenPair[] lockTokens = new LockUriTokenPair[] { new LockUriTokenPair(new Uri(RemoteStoragePath), lockInfo.LockToken) };

                    // Unlock the item in the remote storage.
                    try
                    {
                        await Program.DavClient.UnlockAsync(new Uri(RemoteStoragePath), lockTokens, null, cancellationToken);
                        Logger.LogDebug("Unlocked in the remote storage successfully", UserFileSystemPath, default, operationContext);
                    }
                    catch (ITHit.WebDAV.Client.Exceptions.ConflictException)
                    {
                        Logger.LogDebug("The item is already unlocked.", UserFileSystemPath, default, operationContext);
                    }
                }

                // Delete lock-mode and lock-token info.
                placeholder.TryDeleteLockInfo();
                placeholder.UpdateUI();
            }
        }
        
    }
}
