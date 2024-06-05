using System;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem.Windows;


namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <inheritdoc />
    public abstract class VirtualEngineBase : EngineWindows
    {
        /// <summary>
        /// Currently loged-in user name or user ID. 
        /// </summary>
        /// <remarks>
        /// Used to set lock Owner name as well as to distinguish locks applied
        /// by the currently loged-in user from locks applied by other users, across multiple devices.
        /// 
        /// The default value of the Environment.UserName is used for demo purposes only.
        /// </remarks>
        public string CurrentUserPrincipal { get; set; } = Environment.UserName;

        /// <summary>
        /// Determins if a provied user name is a currently loged-in user.
        /// </summary>
        /// <param name="userName">User name.</param>
        /// <returns>True if user name matches currently loged-in user. False - otherwise.</returns>
        public bool IsCurrentUser(string userName) 
        { 
            return CurrentUserPrincipal.Equals(userName, StringComparison.InvariantCultureIgnoreCase); 
        }

        /// <summary>
        /// Path to the icons folder.
        /// </summary>
        private readonly string iconsFolderPath;

        /// <summary>
        /// Folder that contains images displayed in Status column, context menu, etc. 
        /// </summary>
        public string IconsFolderPath => iconsFolderPath;

        /// <summary>
        /// Mark documents locked by other users as read-only for this user and vice versa.
        /// </summary>
        public readonly bool SetLockReadOnly;

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
        /// <param name="setLockReadOnly">Mark documents locked by other users as read-only for this user and vice versa.</param>
        /// <param name="logFormatter">Formats log output.</param>
        public VirtualEngineBase(
            string license, 
            string userFileSystemRootPath, 
            string remoteStorageRootPath, 
            string iconsFolderPath,
            bool setLockReadOnly,
            LogFormatter logFormatter) 
            : base(license, userFileSystemRootPath)
        {
            this.iconsFolderPath = iconsFolderPath ?? throw new ArgumentNullException(nameof(iconsFolderPath));
            _ = logFormatter ?? throw new ArgumentNullException(nameof(logFormatter));

            // We want our file system to run regardless of any errors.
            // If any request to file system fails in user code or in Engine itself we continue processing.
            ThrowExceptions = false;
            
            SetLockReadOnly = setLockReadOnly;

            StateChanged += Engine_StateChanged;
            ItemsChanged += Engine_ItemsChanged;
            SyncService.StateChanged += SyncService_StateChanged;
            Error += logFormatter.LogError;
            Message += logFormatter.LogMessage;
            Debug += logFormatter.LogDebug;
        }

        
        /// <summary>
        /// Fired for each file or folder change.
        /// </summary>
        private void Engine_ItemsChanged(Engine sender, ItemsChangeEventArgs e)
        {
            foreach (ChangeEventItem item in e.Items)
            {
                // Save custom properties received from the remote storage here.
                // They will be displayed in Windows Explorer columns.
                if (e.Direction == SyncDirection.Incoming && e.Result.IsSuccess)
                {
                    switch (e.OperationType)
                    {
                        case OperationType.Create:
                        //case OperationType.CreateCompletion:
                        case OperationType.Populate:
                        case OperationType.UpdateMetadata:
                            if (item.Metadata != null)
                            {
                                item.Properties.SaveProperties(item.Metadata as FileSystemItemMetadataExt);
                            }
                            break;
                    }
                }

                // If incoming update failed becase a file is in use,
                // try to show merge dialog (for MS Office Word, PowerPoint etc.).
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
            ILogger logger = Logger.CreateLogger(e.ComponentName);
            string msg = $"{e.Direction} {e.OperationType}: {e.Result.Status}";
            switch (e.Result.Status)
            {
                case OperationStatus.Success:
                    switch (e.Direction)
                    {
                        case SyncDirection.Incoming:
                            logger.LogMessage(msg, item.Path, item.NewPath, e.OperationContext);
                            break;
                        case SyncDirection.Outgoing:
                            logger.LogDebug(msg, item.Path, item.NewPath, e.OperationContext);
                            break;
                    }
                    break;
                case OperationStatus.Conflict:
                    logger.LogMessage(msg, item.Path, item.NewPath, e.OperationContext);
                    break;
                case OperationStatus.Exception:
                    logger.LogError(msg, item.Path, item.NewPath, e.Result.Exception);
                    break;
                case OperationStatus.Filtered:
                    msg = $"{msg} by {e.Result.FilteredBy.GetType().Name}";
                    logger.LogDebug(msg, item.Path, item.NewPath, e.OperationContext);
                    break;
                default:
                    msg = $"{msg}. {e.Result.Message}";
                    logger.LogDebug(msg, item.Path, item.NewPath, e.OperationContext);
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

            //if (await new ErrorStatusFilter(true).FilterAsync(direction, operationType, path, itemType, newPath, operationContext))
            //{
            //    return true;
            //}

            return false;
        }

        /// <inheritdoc/>
        public override async Task StartAsync(bool processModified = true, CancellationToken cancellationToken = default)
        {
            await base.StartAsync(processModified, cancellationToken);
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();        
        }

        /// <summary>
        /// Fired on Engine status change.
        /// </summary>
        /// <param name="engine">Engine</param>
        /// <param name="e">Contains new and old Engine state.</param>
        private void Engine_StateChanged(Engine engine, EngineWindows.StateChangeEventArgs e)
        {
            engine.Logger.LogMessage($"{e.NewState}", engine.Path);
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
                SyncService.Logger.LogMessage($"{e.NewState}", this.Path);
            }
        }


        private bool disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {

                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
            base.Dispose(disposing);
        }
    }
}
