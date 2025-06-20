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
        public bool SetLockReadOnly;

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
            ItemsChanging += Engine_ItemsChanging;
            ItemsChanged += Engine_ItemsChanged;
            ItemsProgress += Engine_ItemsProgress;
            SyncService.StateChanged += SyncService_StateChanged;
            Error += logFormatter.LogError;
            Message += logFormatter.LogMessage;
            Debug += logFormatter.LogDebug;
        }

        
        /// <summary>
        /// Fired before items(s) changes on queing, hydration, upload, population progress.
        /// </summary>
        private void Engine_ItemsChanging(Engine sender, ItemsChangeEventArgs e)
        {
            // Log info about the opertion.
            switch (e.OperationType)
            {
                case OperationType.Listing:
                    // Log a single parent folder for folder population.
                    LogItemChanging(e, e.Parent);
                    break;
                default:
                    // Log each item in the list for all other operations.
                    foreach (ChangeEventItem item in e.Items)
                    {
                        LogItemChanging(e, item);
                    }
                    break;
            }
        }

        private void LogItemChanging(ItemsChangeEventArgs e, ChangeEventItem item)
        {
            // Log info about the opertion.
            ILogger logger = Logger.CreateLogger(e.ComponentName);
            string msg = $"{e.Direction} {e.OperationType}:{e.NotificationTime}";

            switch (e.Source)
            {
                case OperationSource.Server:
                    logger.LogDebug(msg, item.Path, item.NewPath, e.OperationContext, item.Metadata);
                    break;
                case OperationSource.Client:
                    logger.LogDebug(msg, item.Path, item.NewPath, e.OperationContext, item.Metadata);
                    break;
            }
        }
        

        
        /// <summary>
        /// Fired during download, upload or population progress.
        /// </summary>
        private void Engine_ItemsProgress(Engine sender, ItemsChangeProgressEventArgs e)
        {
            // Log info about the opertion.
            switch (e.OperationType)
            {
                case OperationType.Listing:
                    // Log a single parent folder for folder population.
                    LogItemProgress(e, e.Parent);
                    break;
                default:
                    // Log each item in the list for all other operations.
                    foreach (ChangeEventItem item in e.Items)
                    {
                        LogItemProgress(e, item);
                    }
                    break;
            }
        }

        private void LogItemProgress(ItemsChangeProgressEventArgs e, ChangeEventItem item)
        {
            // Log info about the opertion.
            ILogger logger = Logger.CreateLogger(e.ComponentName);
            string msg = $"{e.Direction} {e.OperationType}:{e.NotificationTime}";

            // Log progress.                
            long progress = e.Position * 100 / (e.Length > 0 ? e.Length : 1);
            msg = $"{msg}: {progress}%";

            switch (e.Source)
            {
                case OperationSource.Server:
                    logger.LogDebug(msg, item.Path, item.NewPath, e.OperationContext, item.Metadata);
                    break;
                case OperationSource.Client:
                    logger.LogDebug(msg, item.Path, item.NewPath, e.OperationContext, item.Metadata);
                    break;
            }
        }
        

        
        /// <summary>
        /// Fired after file(s) or folder(s) changed.
        /// </summary>
        private void Engine_ItemsChanged(Engine sender, ItemsChangeEventArgs e)
        {
            foreach (ChangeEventItem item in e.Items)
            {
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
            }

            // Log info about the opertion.
            switch (e.OperationType)
            {
                case OperationType.Listing:
                    // Log a single parent folder for folder population.
                    LogItemChanged(e, e.Parent);
                    break;
                default:
                    // Log each item in the list for all other operations.
                    foreach (ChangeEventItem item in e.Items)
                    {
                        LogItemChanged(e, item);
                    }
                    break;
            }
        }

        private void LogItemChanged(ItemsChangeEventArgs e, ChangeEventItem item)
        {
            ILogger logger = Logger.CreateLogger(e.ComponentName);
            string msg = $"{e.Direction} {e.OperationType}:{e.NotificationTime}:{e.Result.Status}";

            switch (e.Result.Status)
            {
                case OperationStatus.Success:
                    switch (e.Source)
                    {
                        case OperationSource.Server:
                            logger.LogMessage(msg, item.Path, item.NewPath, e.OperationContext, item.Metadata);
                            break;
                        case OperationSource.Client:
                            if(e.OperationType == OperationType.Dehydration)
                                logger.LogMessage(msg, item.Path, item.NewPath, e.OperationContext, item.Metadata);
                            else
                                logger.LogDebug(msg, item.Path, item.NewPath, e.OperationContext, item.Metadata);
                            break;
                    }
                    break;
                case OperationStatus.Conflict:
                    logger.LogMessage(msg, item.Path, item.NewPath, e.OperationContext, item.Metadata);
                    break;
                case OperationStatus.Exception:
                    logger.LogError(msg, item.Path, item.NewPath, e.Result.Exception, e.OperationContext, item.Metadata);
                    break;
                case OperationStatus.Filtered:
                    msg = $"{msg} by {e.Result.FilteredBy.GetType().Name}";
                    logger.LogDebug(msg, item.Path, item.NewPath, e.OperationContext, item.Metadata);
                    break;
                default:
                    msg = $"{msg}. {e.Result.Message}";
                    logger.LogDebug(msg, item.Path, item.NewPath, e.OperationContext, item.Metadata);
                    break;
            }
        }
        

        /// <inheritdoc/>
        public override async Task<bool> FilterAsync(SyncDirection direction, OperationType operationType, string path, FileSystemItemType itemType, string newPath, IOperationContext operationContext)
        {
            if (await new AutoLockFilter().FilterAsync(direction, operationType, path, itemType, newPath, operationContext))
            {
                return true;
            }

            if (await new ZipFilter().FilterAsync(direction, operationType, path, itemType, newPath, operationContext))
            {
                return true;
            }

            if (await new MsOfficeFilter().FilterAsync(direction, operationType, path, itemType, newPath, operationContext))
            {
                return true;
            }

            if (await new LibreOfficeFilter().FilterAsync(direction, operationType, path, itemType, newPath, operationContext))
            {
                return true;
            }

            if (await new AutoCadFilter().FilterAsync(direction, operationType, path, itemType, newPath, operationContext))
            {
                return true;
            }

            if (await new PhotoshopFilter().FilterAsync(direction, operationType, path, itemType, newPath, operationContext))
            {
                return true;
            }

            if (await new FoxitFilter().FilterAsync(direction, operationType, path, itemType, newPath, operationContext))
            {
                return true;
            }

            if (await new AutodeskInventorFilter().FilterAsync(direction, operationType, path, itemType, newPath, operationContext))
            {
                return true;
            }

            if (await new AutodeskRevitFilter().FilterAsync(direction, operationType, path, itemType, newPath, operationContext))
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
        public override async Task StartAsync(bool processChanges = true, CancellationToken cancellationToken = default)
        {
            await base.StartAsync(processChanges, cancellationToken);
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
