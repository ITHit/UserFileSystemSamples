using System;
using System.IO;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Synchronization;
using ITHit.FileSystem.Windows;


namespace WebDAVDrive
{
    /// <summary>
    /// Monitors changes in the remote storage, notifies the client and updates the user file system.
    /// </summary>
    /// <remarks>
    /// If any file or folder is created, updated, delated, moved, locked or unlocked in the remote storage, 
    /// calls <see cref="IServerCollectionNotifications.ProcessChangesAsync"/> method.
    /// </remarks>
    internal class RemoteStorageMonitorSyncId : RemoteStorageMonitorBase
    {
        /// <summary>
        /// Sync mode that corresponds with this remote storage monitor type;
        /// </summary>
        public override IncomingSyncMode SyncMode { get { return IncomingSyncMode.SyncId; } }


        /// <summary>
        /// <see cref="Engine"/> instance.
        /// </summary>
        private readonly VirtualEngine engine;

        private readonly string webDAVServerUrl;

        internal RemoteStorageMonitorSyncId(string webSocketServerUrl, string webDAVServerUrl, VirtualEngine engine) 
            : base(webSocketServerUrl, 1, engine.Logger.CreateLogger($"RS Monitor: SyncID"))
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

                string userFileSystemPath = engine.Mapping.ReverseMapPath(remoteStoragePath);
                string userFileSystemParentPath = Path.GetDirectoryName(userFileSystemPath);
                switch (webSocketMessage.EventType)
                {
                    case "created":
                        // Verify that parent folder exists and is not offline.
                        return !((Directory.Exists(userFileSystemParentPath) && !new DirectoryInfo(userFileSystemParentPath).Attributes.HasFlag(FileAttributes.Offline)) ||
                                  engine.Placeholders.IsPinned(userFileSystemParentPath));
                    case "deleted":
                        // Verify that parent folder exists and is not offline.
                        return !Directory.Exists(userFileSystemParentPath)
                            || new DirectoryInfo(userFileSystemParentPath).Attributes.HasFlag(FileAttributes.Offline);

                    case "moved":
                        // Verify that source exists OR target folder exists and is not offline.
                        if (FsPath.Exists(userFileSystemPath))
                        {
                            return false;
                        }
                        else
                        {
                            string remoteStorageNewPath = engine.Mapping.GetAbsoluteUri(webSocketMessage.TargetPath);
                            string userFileSystemNewPath = engine.Mapping.ReverseMapPath(remoteStorageNewPath);
                            string userFileSystemNewParentPath = Path.GetDirectoryName(userFileSystemNewPath);
                            return !Directory.Exists(userFileSystemNewParentPath)
                                || (new DirectoryInfo(userFileSystemNewParentPath).Attributes.HasFlag(FileAttributes.Offline) && (((int)new DirectoryInfo(userFileSystemNewParentPath).Attributes & (int)FileAttributesExt.Pinned) == 0));
                        }

                    case "updated":
                    default:
                        // Any other notifications.
                        return !FsPath.Exists(userFileSystemPath);
                }
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
        protected override async Task ProcessAsync(WebSocketMessage message = null)
        {
            try
            {
                await ServerNotifications.ProcessChangesAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to process changes", WebSocketServerUrl, null, ex);
            }
        }
    }
}
