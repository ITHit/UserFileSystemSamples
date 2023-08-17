using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebDAVDrive
{
    internal class RemoteStorageMonitor : RemoteStorageMonitorBase
    {
        /// <summary>
        /// <see cref="Engine"/> instance.
        /// </summary>
        private readonly VirtualEngine engine;

        internal RemoteStorageMonitor(string webSocketServerUrl, VirtualEngine engine) : base(webSocketServerUrl, engine.Logger.CreateLogger(typeof(RemoteStorageMonitor).Name))
        {
            this.engine = engine;
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
            if (remoteStoragePath.StartsWith(Program.Settings.WebDAVServerUrl, StringComparison.InvariantCultureIgnoreCase))
            {
                Logger.LogDebug($"EventType: {webSocketMessage.EventType}", webSocketMessage.ItemPath, webSocketMessage.TargetPath);

                string userFileSystemPath = Mapping.ReverseMapPath(remoteStoragePath);
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
                        // TODO: incorrect condition
                        if (File.Exists(userFileSystemPath))
                        {
                            return false;
                        }
                        else
                        {
                            string remoteStorageNewPath = Mapping.GetAbsoluteUri(webSocketMessage.TargetPath);
                            string userFileSystemNewPath = Mapping.ReverseMapPath(remoteStorageNewPath);
                            string userFileSystemNewParentPath = Path.GetDirectoryName(userFileSystemNewPath);
                            return !Directory.Exists(userFileSystemNewParentPath)
                                || (new DirectoryInfo(userFileSystemNewParentPath).Attributes.HasFlag(FileAttributes.Offline) && (((int)new DirectoryInfo(userFileSystemNewParentPath).Attributes & (int)FileAttributesExt.Pinned) == 0));
                        }

                    case "updated":
                    default:
                        // Any other notifications.
                        return !File.Exists(userFileSystemPath);
                }
            }

            return true;
        }
    }
}
