using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Synchronization;
using ITHit.FileSystem.Windows;
using ITHit.WebDAV.Client;


namespace WebDAVDrive
{
    /// <summary>
    /// Monitors changes in the remote storage, notifies the client and updates the user file system. 
    /// </summary>
    /// <remarks>
    /// This monitor receives messages about files being created, updated, deleted and moved.
    /// It also performs a complete synchronization via poolin using a <see cref="IncomingPoolingSync.ProcessAsync"/> 
    /// call on application start and web sockets reconnection.
    /// 
    /// If any item changed in the remote storage it calls <see cref="IServerNotifications"/> interfce methods.
    /// </remarks>
    internal class RemoteStorageMonitorCRUD : RemoteStorageMonitorBase
    {
        /// <summary>
        /// Sync mode that corresponds with this remote storage monitor type;
        /// </summary>
        public override IncomingSyncMode SyncMode { get { return IncomingSyncMode.Disabled; } }

        /// <summary>
        /// <see cref="Engine"/> instance.
        /// </summary>
        private readonly VirtualEngine engine;

        private readonly string webDAVServerUrl;

        internal RemoteStorageMonitorCRUD(string webSocketServerUrl, string webDAVServerUrl, VirtualEngine engine) 
            : base(webSocketServerUrl, int.MinValue, engine.Logger.CreateLogger($"RS Monitor: CRUD"))
        {
            this.webDAVServerUrl = webDAVServerUrl;
            this.engine = engine;
        }

        /// <inheritdoc/>
        public override bool Filter(WebSocketMessage webSocketMessage)
        {
            string remoteStoragePath = engine.Mapping.GetAbsoluteUri(webSocketMessage.ItemPath);

            // Just in case there is more than one WebSockets server/virtual folder that
            // is sending notifications (like with webdavserver.net, webdavserver.com),
            // here we filter notifications that come from a different server/virtual folder.
            if (remoteStoragePath.StartsWith(webDAVServerUrl, StringComparison.InvariantCultureIgnoreCase))
            {
                Logger.LogDebug($"EventType: {webSocketMessage.EventType}", webSocketMessage.ItemPath, webSocketMessage.TargetPath);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Triggers <see cref="ISynchronizationCollection.GetChangesAsync"/> call to get 
        /// and process all changes from the remote storage.
        /// </summary>
        /// <param name="message">Information about changes or null in case of Engine start or sockets reconnection.</param>
        /// <remarks>
        /// We do not pass WebSockets cancellation token to this method because stopping 
        /// web sockets should not stop processing changes. 
        /// To stop processing changes that are already received the Engine must be stopped.
        /// </remarks>
        protected override async Task ProcessAsync(WebSocketMessage webSocketMessage = null)
        {
            if(webSocketMessage == null)
            {
                // This is the Engine start, sockets reconnection or authentication event.
                // Performing full sync of loaded items using pooling.
                await engine.SyncService.IncomingPooling.ProcessAsync();
            }
            else
            {
                // The item is created, updated, moved or deleted.
                // Updating a single item.
                await ProcessWebSocketsMessageAsync(webSocketMessage);
            }
        }

        protected async Task ProcessWebSocketsMessageAsync(WebSocketMessage webSocketMessage)
        {
            try
            {
                string remoteStoragePath = engine.Mapping.GetAbsoluteUri(webSocketMessage.ItemPath);

                string userFileSystemPath = engine.Mapping.ReverseMapPath(remoteStoragePath);
                CancellationToken cancellationToken = engine.CancellationTokenSource.Token;
                switch (webSocketMessage.EventType)
                {
                    case "created":
                        await CreateAsync(remoteStoragePath);
                        break;

                    case "updated":
                    case "locked":
                    case "unlocked":
                        IWebDavResponse<IHierarchyItem> resUpdate = await engine.DavClient.GetItemAsync(new Uri(remoteStoragePath), Mapping.GetDavProperties(), null, cancellationToken);
                        IHierarchyItem itemUpdate = resUpdate.WebDavResponse;
                        if (itemUpdate != null)
                        {
                            IMetadata metadataUpdate = Mapping.GetMetadata(itemUpdate);
                            await engine.ServerNotifications(userFileSystemPath).UpdateAsync(metadataUpdate);
                        }
                        break;

                    case "deleted":
                        await engine.ServerNotifications(userFileSystemPath).DeleteAsync();
                        break;

                    case "moved":
                        string remoteStorageNewPath = engine.Mapping.GetAbsoluteUri(webSocketMessage.TargetPath);
                        string userFileSystemNewPath = engine.Mapping.ReverseMapPath(remoteStorageNewPath);
                        OperationResult res = await engine.ServerNotifications(userFileSystemPath).MoveToAsync(userFileSystemNewPath);                        
                        switch (res.Status)
                        {
                            case OperationStatus.NotFound:
                                // Source item is not loaded. Creating the item in the target folder.
                                await CreateAsync(remoteStorageNewPath);
                                break;

                            case OperationStatus.TargetNotFound:
                                // The target parent folder does not exists or is offline, delete the source item.
                                await engine.ServerNotifications(userFileSystemPath).DeleteAsync();
                                break;
                        }
                       
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process:{webSocketMessage.EventType}", webSocketMessage.ItemPath, webSocketMessage.TargetPath, ex);
            }
        }

        private async Task CreateAsync(string remoteStoragePath)
        {
            IWebDavResponse<IHierarchyItem> resCreate = await engine.DavClient.GetItemAsync(new Uri(remoteStoragePath), Mapping.GetDavProperties(), null, engine.CancellationTokenSource.Token);
            IHierarchyItem itemCreate = resCreate.WebDavResponse;
            if (itemCreate != null)
            {
                string userFileSystemPath = engine.Mapping.ReverseMapPath(remoteStoragePath);
                string userFileSystemParentPath = Path.GetDirectoryName(userFileSystemPath);

                IMetadata metadataCreate = Mapping.GetMetadata(itemCreate);
                await engine.ServerNotifications(userFileSystemParentPath).CreateAsync(new[] { metadataCreate });
            }
        }
    }
}
