using Common.Core;
using ITHit.FileSystem;
using WebDAVCommon;

namespace WebDAVMacApp
{
    internal class RemoteStorageMonitor : RemoteStorageMonitorBase
    {

        internal RemoteStorageMonitor(string webSocketServerUrl, ILogger logger) : base(webSocketServerUrl, logger)
        {
            this.InstanceId = Environment.MachineName;
        }

        /// <summary>
        /// Verifies that the WebSockets message is for the item that exists 
        /// in the user file system and should be updated.
        /// </summary>
        /// <param name="webSocketMessage">Information about change in the remote storage.</param>
        /// <returns>True if the item exists and should be updated. False otherwise.</returns>
        public override bool Filter(WebSocketMessage webSocketMessage)
        {
            string remoteStoragePath = Mapping.GetAbsoluteUri(webSocketMessage.ItemPath);

            // Just in case there is more than one WebSockets server/virtual folder that
            // is sending notifications (like with webdavserver.net, webdavserver.com),
            // here we filter notifications that come from a different server/virtual folder.
            if (remoteStoragePath.StartsWith(AppGroupSettings.Settings.Value.WebDAVServerUrl, StringComparison.InvariantCultureIgnoreCase))
            {
                Logger.LogDebug($"EventType: {webSocketMessage.EventType}", webSocketMessage.ItemPath, webSocketMessage.TargetPath);

                return false;
            }

            return true;
        }
    }
}
