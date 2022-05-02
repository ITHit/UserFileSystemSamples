using System;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using log4net;
using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common;

using ITHit.WebDAV.Client;
using System.Threading;

namespace WebDAVDrive
{
    /// <inheritdoc />
    public class VirtualEngine : VirtualEngineBase
    {
        /// <summary>
        /// Monitors changes in the remote storage, notifies the client and updates the user file system.
        /// </summary>
        internal readonly RemoteStorageMonitor RemoteStorageMonitor;

        /// <summary>
        /// Gets or sets a value that indicates whether to send an authenticate header with the websocket.
        /// </summary>
        public NetworkCredential Credentials { get; set; }

        /// <summary>
        /// Creates a vitual file system Engine.
        /// </summary>
        /// <param name="license">A license string.</param>
        /// <param name="userFileSystemRootPath">
        /// A root folder of your user file system. 
        /// Your file system tree will be located under this folder.
        /// </param>
        /// <param name="remoteStorageRootPath">Path to the remote storage root.</param>
        /// <param name="webSocketServerUrl">Web sockets server that sends notifications about changes on the server.</param>
        /// <param name="iconsFolderPath">Path to the icons folder.</param>
        /// <param name="rpcCommunicationChannelName">Channel name to communicate with Windows Explorer context menu and other components on this machine.</param>
        /// <param name="syncIntervalMs">Full synchronization interval in milliseconds.</param>
        /// <param name="maxDegreeOfParallelism">A maximum number of concurrent tasks.</param>
        /// <param name="log4net">Log4net logger.</param>
        public VirtualEngine(
            string license, 
            string userFileSystemRootPath, 
            string remoteStorageRootPath, 
            string webSocketServerUrl, 
            string iconsFolderPath, 
            string rpcCommunicationChannelName,        
            double syncIntervalMs,
            int maxDegreeOfParallelism,
            ILog log4net)
            : base(license, userFileSystemRootPath, remoteStorageRootPath, iconsFolderPath, rpcCommunicationChannelName, syncIntervalMs, maxDegreeOfParallelism, log4net)
        {
            RemoteStorageMonitor = new RemoteStorageMonitor(webSocketServerUrl, this, log4net);
        }

        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(string userFileSystemPath, FileSystemItemType itemType, byte[] itemId)
        {
            if (itemType == FileSystemItemType.File)
            {
                return new VirtualFile(userFileSystemPath, this, this);
            }
            else
            {
                return new VirtualFolder(userFileSystemPath, this, this);
            }
        }

        //public override IMapping Mapping { get { return new Mapping(this); } }

        /// <inheritdoc/>
        public override async Task StartAsync(bool processModified = true, CancellationToken cancellationToken = default)
        {
            await base.StartAsync(processModified, cancellationToken);
            await RemoteStorageMonitor.StartAsync();
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();
            await RemoteStorageMonitor.StopAsync();
        }

        private bool disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    RemoteStorageMonitor.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        public override async Task<byte[]> GetThumbnailAsync(string userFileSystemPath, uint size)
        {
            byte[] thumbnail = null;

            string[] exts = Program.Settings.RequestThumbnailsFor.Trim().Split("|");
            string ext = System.IO.Path.GetExtension(userFileSystemPath).TrimStart('.');

            if (exts.Any(ext.Equals) || exts.Any("*".Equals))
            {
                string ThumbnailGeneratorUrl = Program.Settings.ThumbnailGeneratorUrl.Replace("{thumbnail width}", ""+size).Replace("{thumbnail height}", "" + size);
                string filePathRemote = ThumbnailGeneratorUrl.Replace("{path to file}", WebDAVDrive.Mapping.MapPath(userFileSystemPath));

                try
                {
                    using (IWebResponse response = await Program.DavClient.DownloadAsync(new Uri(filePathRemote)))
                    {
                        using (Stream stream = await response.GetResponseStreamAsync())
                        {
                            thumbnail = await StreamToByteArrayAsync(stream);
                        }
                    }
                }
                catch (WebException we)
                {
                    LogMessage(we.Message, userFileSystemPath);
                }
                catch (Exception e)
                {
                    LogError($"Failed to load thumbnail {size}px", userFileSystemPath, null, e);
                }
            }

            string thumbnailResult = thumbnail != null ? "Success" : "Not Impl";
            LogMessage($"{nameof(VirtualEngine)}.{nameof(GetThumbnailAsync)}() - {thumbnailResult}", userFileSystemPath);

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
        public override async Task<IEnumerable<FileSystemItemPropertyData>> GetItemPropertiesAsync(string userFileSystemPath)
        {
            //LogMessage($"{nameof(VirtualEngine)}.{nameof(GetItemPropertiesAsync)}()", userFileSystemPath);

            IList<FileSystemItemPropertyData> props = new List<FileSystemItemPropertyData>();

            PlaceholderItem placeholder = this.Placeholders.GetItem(userFileSystemPath);

            // Read LockInfo and choose the lock icon.
            string lockIconName = null;
            if (placeholder.Properties.TryGetValue("LockInfo", out IDataItem propLockInfo))
            {
                // The file is locked by this user.
                lockIconName = "Locked.ico";
            }
            else if (placeholder.Properties.TryGetValue("ThirdPartyLockInfo", out propLockInfo))
            {
                // The file is locked by somebody else on the server.
                lockIconName = "LockedByAnotherUser.ico";
            }

            if (propLockInfo != null && propLockInfo.TryGetValue<ServerLockInfo>(out ServerLockInfo lockInfo))
            {

                // Get Lock Owner.
                FileSystemItemPropertyData propertyLockOwner = new FileSystemItemPropertyData()
                {
                    Id = (int)CustomColumnIds.LockOwnerIcon,
                    Value = lockInfo.Owner,
                    IconResource = System.IO.Path.Combine(this.IconsFolderPath, lockIconName)
                };
                props.Add(propertyLockOwner);

                // Get Lock Expires.
                FileSystemItemPropertyData propertyLockExpires = new FileSystemItemPropertyData()
                {
                    Id = (int)CustomColumnIds.LockExpirationDate,
                    Value = lockInfo.LockExpirationDateUtc.ToString(),
                    IconResource = System.IO.Path.Combine(this.IconsFolderPath, "Empty.ico")
                };
                props.Add(propertyLockExpires);
            }


            // Read LockMode.
            if (placeholder.Properties.TryGetValue("LockMode", out IDataItem propLockMode))
            {
                if (propLockMode.TryGetValue<LockMode>(out LockMode lockMode) && lockMode != LockMode.None)
                {
                    FileSystemItemPropertyData propertyLockMode = new FileSystemItemPropertyData()
                    {
                        Id = (int)CustomColumnIds.LockScope,
                        Value = "Locked",
                        IconResource = System.IO.Path.Combine(this.IconsFolderPath, "Empty.ico")
                    };
                    props.Add(propertyLockMode);
                }
            }

            // Read ETag.
            if (placeholder.Properties.TryGetValue("ETag", out IDataItem propETag))
            {
                if (propETag.TryGetValue<string>(out string eTag))
                {
                    FileSystemItemPropertyData propertyETag = new FileSystemItemPropertyData()
                    {
                        Id = (int)CustomColumnIds.ETag,
                        Value = eTag,
                        IconResource = System.IO.Path.Combine(this.IconsFolderPath, "Empty.ico")
                    };
                    props.Add(propertyETag);
                }
            }

            return props;
        }
    }
}
