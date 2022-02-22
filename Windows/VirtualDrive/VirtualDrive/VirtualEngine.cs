using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using log4net;
using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;

namespace VirtualDrive
{
    /// <inheritdoc />
    public class VirtualEngine : VirtualEngineBase
    {
        /// <summary>
        /// Monitors changes in the remote storage, notifies the client and updates the user file system.
        /// </summary>
        public readonly RemoteStorageMonitor RemoteStorageMonitor;

        /// <summary>
        /// Creates a vitual file system Engine.
        /// </summary>
        /// <param name="license">A license string.</param>
        /// <param name="userFileSystemRootPath">
        /// A root folder of your user file system. 
        /// Your file system tree will be located under this folder.
        /// </param>
        /// <param name="remoteStorageRootPath">Path to the remote storage root.</param>
        /// <param name="iconsFolderPath">Path to the icons folder.</param>
        /// <param name="rpcCommunicationChannelName">Channel name to communicate with Windows Explorer context menu and other components on this machine.</param>
        /// <param name="syncIntervalMs">Full synchronization interval in milliseconds.</param>
        /// <param name="log4net">Log4net logger.</param>
        public VirtualEngine(
            string license, 
            string userFileSystemRootPath, 
            string remoteStorageRootPath, 
            string iconsFolderPath, 
            string rpcCommunicationChannelName, 
            double syncIntervalMs,
            ILog log4net)
            : base(license, userFileSystemRootPath, remoteStorageRootPath, iconsFolderPath, rpcCommunicationChannelName, syncIntervalMs, log4net)
        {
            RemoteStorageMonitor = new RemoteStorageMonitor(remoteStorageRootPath, this, log4net);
        }

        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(string userFileSystemPath, FileSystemItemType itemType, byte[] remoteStorageItemId)
        {
            if (itemType == FileSystemItemType.File)
            {
                return new VirtualFile(userFileSystemPath, remoteStorageItemId, this, this);
            }
            else
            {
                return new VirtualFolder(userFileSystemPath, remoteStorageItemId, this, this);
            }
        }

        //public override IMapping Mapping { get { return new Mapping(this); } }

        /// <inheritdoc/>
        public override async Task StartAsync()
        {
            await base.StartAsync();
            RemoteStorageMonitor.Start();
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();
            RemoteStorageMonitor.Stop();
        }

        /// <inheritdoc/>
        public override async Task<byte[]> GetThumbnailAsync(string userFileSystemPath, uint size)
        {
            byte[] thumbnail = ThumbnailExtractor.GetThumbnail(userFileSystemPath, size);

            string thumbnailResult = thumbnail != null ? "Success" : "Not Impl";
            LogMessage($"{nameof(VirtualEngine)}.{nameof(GetThumbnailAsync)}() - {thumbnailResult}", userFileSystemPath);

            return thumbnail;
        }

        /// <inheritdoc/>
        public override async Task<IEnumerable<FileSystemItemPropertyData>> GetItemPropertiesAsync(string userFileSystemPath)
        {
            //LogMessage($"{nameof(VirtualEngine)}.{nameof(GetItemPropertiesAsync)}()", userFileSystemPath);

            IList<FileSystemItemPropertyData> props = new List<FileSystemItemPropertyData>();

            PlaceholderItem placeholder = this.Placeholders.GetItem(userFileSystemPath);

            // Read LockInfo.
            if (placeholder.Properties.TryGetValue("LockInfo", out IDataItem propLockInfo))
            {
                if (propLockInfo.TryGetValue<ServerLockInfo>(out ServerLockInfo lockInfo))
                {
                    // Get Lock Owner.
                    FileSystemItemPropertyData propertyLockOwner = new FileSystemItemPropertyData()
                    {
                        Id = (int)CustomColumnIds.LockOwnerIcon,
                        Value = lockInfo.Owner,
                        IconResource = System.IO.Path.Combine(this.IconsFolderPath, "Locked.ico")
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
    }
}
