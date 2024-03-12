using System;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Samples.Common;


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
        /// Maps remote storage path to the user file system path and vice versa. 
        /// </summary>
        internal readonly Mapping Mapping;

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
            Mapping = new Mapping(Path, remoteStorageRootPath);

            // We want our file system to run regardless of any errors.
            // If any request to file system fails in user code or in Engine itself we continue processing.
            ThrowExceptions = false;

            StateChanged += Engine_StateChanged;
            ItemsChanged += Engine_ItemsChanged;
            SyncService.StateChanged += SyncService_StateChanged;
            Error += logFormatter.LogError;
            Message += logFormatter.LogMessage;
            Debug += logFormatter.LogDebug;

            RemoteStorageMonitor = new RemoteStorageMonitor(remoteStorageRootPath, this, this.Logger);
        }

        /// <summary>
        /// Fired for each file or folder change.
        /// </summary>
        private void Engine_ItemsChanged(Engine sender, ItemsChangeEventArgs e)
        {
            var logger = Logger.CreateLogger(e.ComponentName);
            foreach (ChangeEventItem item in e.Items)
            {

                // If incoming update failed becase a file is in use,
                // try to show merge dialog (for MS Office, etc.).
                if (e.Direction == SyncDirection.Incoming
                    && e.OperationType == OperationType.UpdateContent)
                {
                    switch (e.Result.Status)
                    {
                        case OperationStatus.FileInUse:
                            ITHit.FileSystem.Windows.AppHelper.Utilities.TryNotifyUpdateAvailable(item.Path, e.Result.ShadowFilePath);
                            break;
                    }
                }

                // Log info about the opertion.
                LogItemChange(e, item);
            }
        }

        private void LogItemChange(ItemsChangeEventArgs e, ChangeEventItem item)
        {
            var logger = Logger.CreateLogger(e.ComponentName);

            switch (e.Result.Status)
            {
                case OperationStatus.Success:
                    switch (e.Direction)
                    {
                        case SyncDirection.Incoming:
                            logger.LogMessage($"{e.Direction} {e.OperationType}: {e.Result.Status}", item.Path, item.NewPath, e.OperationContext);
                            break;
                        case SyncDirection.Outgoing:
                            logger.LogDebug($"{e.Direction} {e.OperationType}: {e.Result.Status}", item.Path, item.NewPath, e.OperationContext);
                            break;
                    }
                    break;
                case OperationStatus.Conflict:
                    logger.LogMessage($"{e.Direction} {e.OperationType}: {e.Result.Status}", item.Path, item.NewPath, e.OperationContext);
                    break;
                case OperationStatus.Exception:
                    logger.LogError($"{e.Direction} {e.OperationType}", item.Path, item.NewPath, e.Result.Exception);
                    break;
                case OperationStatus.Filtered:
                    logger.LogDebug($"{e.Direction} {e.OperationType}: {e.Result.Status} by {e.Result.FilteredBy.GetType().Name}", item.Path, item.NewPath, e.OperationContext);
                    break;
                default:
                    logger.LogDebug($"{e.Direction} {e.OperationType}: {e.Result.Status}. {e.Result.Message}", item.Path, item.NewPath, e.OperationContext);
                    break;
            }
        }

        /// <inheritdoc/>
        public override async Task<bool> FilterAsync(SyncDirection direction, OperationType operationType, string path, FileSystemItemType itemType, string newPath, IOperationContext operationContext)
        {

            if (await new ZipFilter().FilterAsync(direction, operationType, path, itemType, newPath, operationContext))
            {
                return true;
            }

            if (await new MsOfficeFilter().FilterAsync(direction, operationType, path, itemType, newPath, operationContext))
            {
                return true;
            }

            if (await new AutoCadFilter().FilterAsync(direction, operationType, path, itemType, newPath, operationContext))
            {
                return true;
            }

            if (await new ErrorStatusFilter().FilterAsync(direction, operationType, path, itemType, newPath, operationContext))
            {
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(byte[] remoteStorageItemId, FileSystemItemType itemType, IContext context, ILogger logger = null)
        {
            string userFileSystemPath = context.FileNameHint;
            if (itemType == FileSystemItemType.File)
            {
                return new VirtualFile(Mapping, userFileSystemPath, logger);
            }
            else
            {
                return new VirtualFolder(Mapping, userFileSystemPath, logger);
            }
        }

        /// <inheritdoc/>
        public override async Task<IMenuCommand> GetMenuCommandAsync(Guid menuGuid, IOperationContext operationContext = null)
        {
            // For this method to be called you need to register a menu command handler.
            // See method description for more details.

            throw new NotImplementedException();
        }

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

        /// <summary>
        /// Show status change.
        /// </summary>
        /// <param name="engine">Engine</param>
        /// <param name="e">Contains new and old Engine state.</param>
        private void Engine_StateChanged(Engine engine, EngineWindows.StateChangeEventArgs e)
        {
            engine.Logger.LogMessage($"{e.NewState}");
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
