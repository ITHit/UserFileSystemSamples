using System.IO;
using System.Threading.Tasks;
using log4net;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;

using ITHit.FileSystem.Samples.Common.Windows;
using System.Threading;
using System;

namespace VirtualFileSystem
{
    
    /// <inheritdoc />
    public class VirtualEngine : EngineWindows
    {
        /// <summary>
        /// Monitors changes in the remote storage, notifies the client and updates the user file system.
        /// </summary>
        internal RemoteStorageMonitor RemoteStorageMonitor;

        /// <summary>
        /// Creates a vitual file system Engine.
        /// </summary>
        /// <param name="license">A license string.</param>
        /// <param name="userFileSystemRootPath">
        /// A root folder of your user file system. 
        /// Your file system tree will be located under this folder.
        /// </param>
        /// <param name="maxDegreeOfParallelism">A maximum number of concurrent tasks.</param>
        /// <param name="log4net">Log4net Logger.</param>
        public VirtualEngine(string license, string userFileSystemRootPath, string remoteStorageRootPath, LogFormatter logFormatter) :
            base(license, userFileSystemRootPath)
        {

            // We want our file system to run regardless of any errors.
            // If any request to file system fails in user code or in Engine itself we continue processing.
            ThrowExceptions = false;

            StateChanged += Engine_StateChanged;
            SyncService.StateChanged += SyncService_StateChanged;
            Error += logFormatter.LogError;
            Message += logFormatter.LogMessage;
            Debug += logFormatter.LogDebug;

            RemoteStorageMonitor = new RemoteStorageMonitor(remoteStorageRootPath, this, this.Logger);
        }

        /// <inheritdoc/>
        public override async Task<bool> FilterAsync(SyncDirection direction, OperationType operationType, string path, FileSystemItemType itemType, string newPath = null, FileAttributes? attributes = null, IOperationContext operationContext = null)
        {
            // Use the code below to filter based on files and folders attributes.
            //if (await new AttributesFilter(FileAttributes.Hidden | FileAttributes.Temporary).FilterAsync(direction, operationType, path, itemType, newPath, attributes))
            //{
            //    LogDebug($"{nameof(AttributesFilter)} filtered {operationType}", path, newPath, operationContext);
            //    return true;
            //}

            if (await new ZipFilter().FilterAsync(direction, operationType, path, itemType, newPath, attributes))
            {
                LogDebug($"{nameof(ZipFilter)} filtered {operationType}", path, newPath, operationContext);
                return true;
            }

            if (await new MsOfficeFilter().FilterAsync(direction, operationType, path, itemType, newPath, attributes))
            {
                LogDebug($"{nameof(MsOfficeFilter)} filtered {operationType}", path, newPath, operationContext);
                return true;
            }

            if (await new AutoCadFilter().FilterAsync(direction, operationType, path, itemType, newPath, attributes))
            {
                LogDebug($"{nameof(AutoCadFilter)} filtered {operationType}", path, newPath, operationContext);
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(string userFileSystemPath, FileSystemItemType itemType, byte[] remoteStorageItemId = null, ILogger logger = null)
        {
            if (itemType == FileSystemItemType.File)
            {
                return new VirtualFile(userFileSystemPath, remoteStorageItemId, logger);
            }
            else
            {
                return new VirtualFolder(userFileSystemPath, remoteStorageItemId, logger);
            }
        }

        /// <inheritdoc/>
        public override async Task<IMenuCommand> GetMenuCommandAsync(Guid menuGuid)
        {
            // For this method to be called you need to register a menu command handler.
            // See method description for more details.

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override async Task StartAsync(bool processModified = true, CancellationToken cancellationToken = default)
        {
            await base.StartAsync(processModified, cancellationToken);
            RemoteStorageMonitor.Start();
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();
            RemoteStorageMonitor.Stop();
        }

        /// <summary>
        /// Show status change.
        /// </summary>
        /// <param name="engine">Engine</param>
        /// <param name="e">Contains new and old Engine state.</param>
        private void Engine_StateChanged(Engine engine, EngineWindows.StateChangeEventArgs e)
        {
            engine.LogMessage($"{e.NewState}");
        }

        /// <summary>
        /// Fired on sync service status change.
        /// </summary>
        /// <param name="sender">Sync service.</param>
        /// <param name="e">Contains new and old sync service state.</param>
        private void SyncService_StateChanged(object sender, SynchEventArgs e)
        {
            if (e.NewState == SynchronizationState.Enabled || e.NewState == SynchronizationState.Disabled)
            {
                SyncService.Logger.LogMessage($"{e.NewState}");
            }
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
