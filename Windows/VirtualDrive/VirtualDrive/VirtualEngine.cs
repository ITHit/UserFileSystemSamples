using System.IO;
using System.Threading.Tasks;
using log4net;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;

using ITHit.FileSystem.Samples.Common.Windows;
using System;

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
        /// Gprc server control to communicate with Windows Explorer 
        /// context menu and other components on this machine.
        /// </summary>
        private readonly GrpcServer grpcServer;

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
            grpcServer = new GrpcServer(rpcCommunicationChannelName, this, log4net);
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
            grpcServer.Start();
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();
            RemoteStorageMonitor.Stop();
            grpcServer.Stop();
        }

        private bool disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    RemoteStorageMonitor.Dispose();
                    grpcServer.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
            base.Dispose(disposing);
        }
    }
}
