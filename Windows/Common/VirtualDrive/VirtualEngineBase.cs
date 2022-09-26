using System;
using System.IO;
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
        /// Path to the icons folder.
        /// </summary>
        private readonly string iconsFolderPath;

        /// <summary>
        /// Folder that contains images displayed in Status column, context menu, etc. 
        /// </summary>
        public string IconsFolderPath => iconsFolderPath;

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
        public VirtualEngineBase(
            string license, 
            string userFileSystemRootPath, 
            string remoteStorageRootPath, 
            string iconsFolderPath, 
            LogFormatter logFormatter) 
            : base(license, userFileSystemRootPath)
        {
            this.iconsFolderPath = iconsFolderPath ?? throw new ArgumentNullException(nameof(iconsFolderPath));
            _ = logFormatter ?? throw new ArgumentNullException(nameof(logFormatter));

            // We want our file system to run regardless of any errors.
            // If any request to file system fails in user code or in Engine itself we continue processing.
            ThrowExceptions = false;

            StateChanged += Engine_StateChanged;
            SyncService.StateChanged += SyncService_StateChanged;
            Error += logFormatter.LogError;
            Message += logFormatter.LogMessage;
            Debug += logFormatter.LogDebug;
        }

        /// <inheritdoc/>
        public override async Task<bool> FilterAsync(SyncDirection direction, OperationType operationType, string path, FileSystemItemType itemType, string newPath = null, IOperationContext operationContext = null)
        {

            if (await new ZipFilter().FilterAsync(direction, operationType, path, itemType, newPath))
            {
                Logger.LogDebug($"{nameof(ZipFilter)} filtered {operationType}", path, newPath, operationContext);
                return true;
            }

            if (await new MsOfficeFilter().FilterAsync(direction, operationType, path, itemType, newPath))
            {
                Logger.LogDebug($"{nameof(MsOfficeFilter)} filtered {operationType}", path, newPath, operationContext);
                return true;
            }

            if (await new AutoCadFilter().FilterAsync(direction, operationType, path, itemType, newPath))
            {
                Logger.LogDebug($"{nameof(AutoCadFilter)} filtered {operationType}", path, newPath, operationContext);
                return true;
            }

            if (await new ErrorStatusFilter().FilterAsync(direction, operationType, path, itemType, newPath))
            {
                Logger.LogDebug($"{nameof(ErrorStatusFilter)} filtered {operationType}", path, newPath, operationContext);
                return true;
            }

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

                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
            base.Dispose(disposing);
        }
    }
}
