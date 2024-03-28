using Common.Core;
using ITHit.FileSystem;
using ITHit.WebDAV.Client;
using Client = ITHit.WebDAV.Client;

using WebDAVCommon;
using FileProvider;

namespace WebDAVMacApp
{
    internal class RemoteStorageMonitor : RemoteStorageMonitorBase
    {       

        internal RemoteStorageMonitor(string webDAVServerUrl, string webSocketServerUrl, NSFileProviderManager fileProviderManager, ILogger logger) :
            base(webDAVServerUrl, webSocketServerUrl, fileProviderManager, logger)
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
            string remoteStoragePath = Mapping.GetAbsoluteUri(webSocketMessage.ItemPath, WebDAVServerUrl);

            // Just in case there is more than one WebSockets server/virtual folder that
            // is sending notifications (like with webdavserver.net, webdavserver.com),
            // here we filter notifications that come from a different server/virtual folder.
            if (remoteStoragePath.StartsWith(WebDAVServerUrl, StringComparison.InvariantCultureIgnoreCase))
            {
                Logger.LogDebug($"EventType: {webSocketMessage.EventType}", webSocketMessage.ItemPath, webSocketMessage.TargetPath);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Indicates if the root folder supports Collection Synchronization implementation.
        /// </summary>
        /// <returns>True if the WebDav server supports Collection Synchronization. False otherwise.</returns>
        public override async Task<bool> IsSyncCollectionSupportedAsync()
        {
            try
            {
                using (WebDavSession webDavSession = await WebDavSessionUtils.GetWebDavSessionAsync())
                {
                    Client.PropertyName[] propNames = new Client.PropertyName[1];
                    propNames[0] = new Client.PropertyName("supported-report-set", "DAV:");
                    Client.IHierarchyItem rootFolder = (await webDavSession.GetItemAsync(WebDAVServerUrl, propNames)).WebDavResponse;

                    return rootFolder.Properties.Any(p => p.Name.Name == "supported-report-set" && p.StringValue.Contains("sync-collection"));
                }
            }
            catch(ITHit.WebDAV.Client.Exceptions.WebDavHttpException)
            {
                return false;
            }
        }
    }
}
