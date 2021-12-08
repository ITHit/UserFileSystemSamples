using System;
using System.Threading.Tasks;
using log4net;
using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Samples.Common.Windows.Rpc;

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
        /// <param name="serverDataFolderPath">Path to the folder that stores custom data associated with files and folders.</param>
        /// <param name="iconsFolderPath">Path to the icons folder.</param>
        /// <param name="rpcCommunicationChannelName">Channel name to communicate with Windows Explorer context menu and other components on this machine.</param>
        /// <param name="syncIntervalMs">Full synchronization interval in milliseconds.</param>
        /// <param name="log4net">Log4net logger.</param>
        public VirtualEngine(
            string license, 
            string userFileSystemRootPath, 
            string remoteStorageRootPath, 
            string serverDataFolderPath, 
            string iconsFolderPath, 
            string rpcCommunicationChannelName, 
            double syncIntervalMs,
            ILog log4net)
            : base(license, userFileSystemRootPath, remoteStorageRootPath, serverDataFolderPath, iconsFolderPath, rpcCommunicationChannelName, syncIntervalMs, log4net)
        {
            RemoteStorageMonitor = new RemoteStorageMonitor(remoteStorageRootPath, this, log4net);
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

        public override IMapping Mapping { get { return new Mapping(this); } }

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

        /// <summary>
        /// Returns thumbnail for specified path in the user file system.
        /// </summary>
        /// <remarks>
        /// Throw <see cref="NotImplementedException"/> if thumbnail is not available.
        /// You may also return empty array of null as indication of non existed thumbnail.
        /// </remarks>
        /// <param name="userFileSystemPath">Path in user file system.</param>
        /// <param name="size">Thumbnail size in pixels.</param>
        /// <returns>Thumbnail bitmap or null if the thumbnail handler is not found.</returns>
        public override async Task<byte[]> GetThumbnailAsync(string userFileSystemPath, uint size)
        {
            return ThumbnailExtractor.GetThumbnail(userFileSystemPath, size);
        }
    }
}
