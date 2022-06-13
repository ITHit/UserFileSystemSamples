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
        /// Monitors changes in the remote storage, notifies the client and updates the user file system.
        /// </summary>
        //  public readonly RemoteStorageMonitor RemoteStorageMonitor;

        /// <summary>
        /// Path to the icons folder.
        /// </summary>
        private readonly string iconsFolderPath;

        /// <summary>
        /// Folder that contains images displayed in Status column, context menu, etc. 
        /// </summary>
        public string IconsFolderPath => iconsFolderPath;

        //public abstract IMapping Mapping { get; }

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
            this.iconsFolderPath = iconsFolderPath ?? throw new NullReferenceException(nameof(iconsFolderPath));

            // We want our file system to run regardless of any errors.
            // If any request to file system fails in user code or in Engine itself we continue processing.
            ThrowExceptions = false;

            StateChanged += Engine_StateChanged;
            Error += logFormatter.LogError;
            Message += logFormatter.LogMessage;
            Debug += logFormatter.LogDebug;
            SyncService.StateChanged += SyncService_StateChanged;

            //RemoteStorageMonitor = new RemoteStorageMonitor(remoteStorageRootPath, this, log4net);
        }

        /// <inheritdoc/>
        public override async Task<bool> FilterAsync(OperationType operationType, string userFileSystemPath, string userFileSystemNewPath = null, IOperationContext operationContext = null)
        {
            try 
            { 
                switch(operationType)
                {
                    // To send file content to the remote storage only when the MS Office or
                    // AutoCAD document is closed, uncommnt the Create and Update cases below.
                    //case OperationType.Create:
                    //case OperationType.Update:

                    case OperationType.Unlock:
                        return FilterHelper.AvoidSync(userFileSystemPath) 
                            || FilterHelper.IsAppLocked(userFileSystemPath);

                    case OperationType.Move:
                    case OperationType.MoveCompletion:
                        // When a hydrated file is deleted, it is moved to a Recycle Bin.
                        return FilterHelper.IsRecycleBin(userFileSystemNewPath) 
                            || FilterHelper.AvoidSync(userFileSystemNewPath);

                    default:
                        return FilterHelper.AvoidSync(userFileSystemPath);
                }
            }
            catch(FileNotFoundException)
            {
                // Typically the file is not found in case of some temporary file that is being deleted.
                // We do not want to continue processing this file, and we do not want any exceptions in the log as this is a normal behaviour.
                LogDebug($"{nameof(IEngine)}.{nameof(FilterAsync)}(): Item not found", userFileSystemPath, userFileSystemNewPath, operationContext);
                return true;
            }
        }

        /// <inheritdoc/>
        public override async Task StartAsync(bool processModified = true, CancellationToken cancellationToken = default)
        {
            await base.StartAsync(processModified, cancellationToken);
            //RemoteStorageMonitor.Start();            
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();
            //RemoteStorageMonitor.Stop();            
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
        private void SyncService_StateChanged(SyncService sender, SynchEventArgs e)
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
                    //RemoteStorageMonitor.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
            base.Dispose(disposing);
        }
    }
}
