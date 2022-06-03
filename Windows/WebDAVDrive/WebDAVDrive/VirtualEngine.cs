using System;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;


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
        /// <param name="syncIntervalMs">Full synchronization interval in milliseconds.</param>
        /// <param name="log4net">Log4net logger.</param>
        public VirtualEngine(
            string license, 
            string userFileSystemRootPath, 
            string remoteStorageRootPath, 
            string webSocketServerUrl, 
            string iconsFolderPath, 
            double syncIntervalMs,
            LogFormatter logFormatter)
            : base(license, userFileSystemRootPath, remoteStorageRootPath, iconsFolderPath, syncIntervalMs, logFormatter)
        {
            RemoteStorageMonitor = new RemoteStorageMonitor(webSocketServerUrl, this, this.Logger);
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

        /// <inheritdoc/>
        public override async Task<IMenuCommand> GetMenuCommandAsync(Guid menuGuid)
        {
            // For this method to be called you need to register a menu command handler.
            // See method description for more details.

            Logger.LogDebug($"{nameof(IEngine)}.{nameof(GetMenuCommandAsync)}()", menuGuid.ToString());

            Guid menuCommandLockGuid = typeof(WebDAVDrive.ShellExtension.ContextMenusProvider).GUID;

            if (menuGuid == menuCommandLockGuid)
            {
                return new MenuCommandLock(this, this.Logger);
            }

            Logger.LogError($"Menu not found", menuGuid.ToString());
            throw new NotImplementedException();
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
    }
}
