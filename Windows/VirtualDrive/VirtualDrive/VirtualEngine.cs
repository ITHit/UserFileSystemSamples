using System;
using System.Threading.Tasks;
using System.Threading;

using ITHit.FileSystem;
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

        //public override IMapping Mapping { get { return new Mapping(this); } }

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
        /// <param name="logFormatter">Logger.</param>
        public VirtualEngine(
            string license, 
            string userFileSystemRootPath, 
            string remoteStorageRootPath, 
            string iconsFolderPath, 
            LogFormatter logFormatter)
            : base(license, userFileSystemRootPath, remoteStorageRootPath, iconsFolderPath, logFormatter)
        {
            RemoteStorageMonitor = new RemoteStorageMonitor(remoteStorageRootPath, this, this.Logger);
        }

        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(string userFileSystemPath, FileSystemItemType itemType, byte[] remoteStorageItemId, ILogger logger = null)
        {
            if (itemType == FileSystemItemType.File)
            {
                return new VirtualFile(userFileSystemPath, remoteStorageItemId, this, logger);
            }
            else
            {
                return new VirtualFolder(userFileSystemPath, remoteStorageItemId, this, logger);
            }
        }

        
        /// <inheritdoc/>
        public override async Task<IMenuCommand> GetMenuCommandAsync(Guid menuGuid)
        {
            // For this method to be called you need to register a menu command handler.
            // See method description for more details.

            Logger.LogDebug($"{nameof(IEngine)}.{nameof(GetMenuCommandAsync)}()", menuGuid.ToString());

            Guid menuCommandLockGuid = typeof(VirtualDrive.ShellExtension.ContextMenuVerbRpc).GUID;

            if (menuGuid == menuCommandLockGuid)
            {
                return new MenuCommandLock(this, this.Logger);
            }

            Logger.LogError($"Menu not found", menuGuid.ToString());
            throw new NotImplementedException();
        }
        

        /// <inheritdoc/>
        public override async Task StartAsync(bool processModified = true, CancellationToken cancellationToken = default)
        {
            await base.StartAsync(processModified, cancellationToken);
            RemoteStorageMonitor.Start();
        }

        /// <inheritdoc/>
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
    }
}
