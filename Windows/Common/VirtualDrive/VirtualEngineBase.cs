using System;
using System.IO;
using System.Threading.Tasks;
using log4net;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Samples.Common.Windows.Rpc;

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
        /// Monitors documents renames and attributes changes in the user file system. 
        /// Required for transactional saves performed by MS Office, AutoCAD, as well as for Notepad++, etc.
        /// </summary>
        public readonly FilteredDocsMonitor FilteredDocsMonitor;

        /// <summary>
        /// Full synchronization service.
        /// In case any changes are lost (app restart, lost connection, etc.) this service will sync all changes.
        /// </summary>
        public readonly FullSyncService SyncService;

        /// <summary>
        /// Logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Path to the folder that stores custom data associated with files and folders.
        /// </summary>
        private readonly string serverDataFolderPath;

        /// <summary>
        /// Path to the icons folder.
        /// </summary>
        private readonly string iconsFolderPath;

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
        public VirtualEngineBase(
            string license, 
            string userFileSystemRootPath, 
            string remoteStorageRootPath, 
            string serverDataFolderPath, 
            string iconsFolderPath, 
            string rpcCommunicationChannelName,
            double syncIntervalMs,
            ILog log4net) 
            : base(license, userFileSystemRootPath)
        {
            logger = new Logger("File System Engine", log4net) ?? throw new NullReferenceException(nameof(log4net));
            this.serverDataFolderPath = serverDataFolderPath ?? throw new NullReferenceException(nameof(serverDataFolderPath));
            this.iconsFolderPath = iconsFolderPath ?? throw new NullReferenceException(nameof(iconsFolderPath));
            this.grpcServer = new GrpcServer(rpcCommunicationChannelName, this, log4net);

            // We want our file system to run regardless of any errors.
            // If any request to file system fails in user code or in Engine itself we continue processing.
            ThrowExceptions = false;

            StateChanged += Engine_StateChanged;
            Error += Engine_Error;
            Message += Engine_Message;

            //RemoteStorageMonitor = new RemoteStorageMonitor(remoteStorageRootPath, this, log4net);
            FilteredDocsMonitor = new FilteredDocsMonitor(userFileSystemRootPath, this, log4net);
            SyncService = new FullSyncService(syncIntervalMs, userFileSystemRootPath, this, log4net);
        }

        public abstract IMapping Mapping { get; }

        /// <inheritdoc/>
        public override async Task<bool> FilterAsync(OperationType operationType, string userFileSystemPath, string userFileSystemNewPath = null, IOperationContext operationContext = null)
        {
            try 
            { 
                switch(operationType)
                {
                    case OperationType.Update:
                    case OperationType.Unlock:
                        // PowerPoint does not block the file for reading when the file is opened for editing.
                        // As a result the file will be sent to the remote storage during each file save operation.
                        // This also improves performance of the file save including for AutoCAD files.
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
            catch(FileNotFoundException ex)
            {
                // Typically the file is not found in case of some temporary file that is being deleted.
                // We do not want to continue processing this file, and we do not want any exceptions in the log as this is a normal behaviour.
                LogMessage(ex.Message, userFileSystemPath, userFileSystemNewPath, operationContext);
                return true;
            }
        }

        /// <inheritdoc/>
        public override async Task StartAsync()
        {
            await base.StartAsync();
            //RemoteStorageMonitor.Start();
            FilteredDocsMonitor.Start();
            await SyncService.StartAsync();
            grpcServer.Start();
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();
            //RemoteStorageMonitor.Stop();
            FilteredDocsMonitor.Stop();
            await SyncService.StopAsync();
            grpcServer.Stop();
        }

        private void Engine_Message(IEngine sender, EngineMessageEventArgs e)
        {
            logger.LogMessage(e.Message, e.SourcePath, e.TargetPath, e.OperationContext);
        }

        private void Engine_Error(IEngine sender, EngineErrorEventArgs e)
        {
            logger.LogError(e.Message, e.SourcePath, e.TargetPath, e.Exception, e.OperationContext);
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
        /// Manages custom data associated with the item and stored outside of the item. 
        /// </summary>
        public ExternalDataManager ExternalDataManager(string userFileSystemPath, ILogger logger = null)
        {
            return new ExternalDataManager(userFileSystemPath, serverDataFolderPath, Path, iconsFolderPath, logger ?? this.logger);
        }

        /// <summary>
        /// Returns thumbnail.
        /// </summary>
        public abstract Task<byte[]> GetThumbnailAsync(string path, uint size);

        private bool disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //RemoteStorageMonitor.Dispose();
                    FilteredDocsMonitor.Dispose();
                    SyncService.Dispose();
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
