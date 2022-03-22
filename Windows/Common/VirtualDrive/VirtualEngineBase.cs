using System;
using System.IO;
using System.Threading.Tasks;
using log4net;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Samples.Common.Windows.Rpc;
using System.Collections.Generic;

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
        /// Full synchronization service.
        /// In case any changes are lost (app restart, lost connection, etc.) this service will sync all changes.
        /// </summary>
        public readonly FullSyncService SyncService;

        /// <summary>
        /// Logger.
        /// </summary>
        private readonly ILogger logger;

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
        /// <param name="iconsFolderPath">Path to the icons folder.</param>
        /// <param name="rpcCommunicationChannelName">Channel name to communicate with Windows Explorer context menu and other components on this machine.</param>
        /// <param name="syncIntervalMs">Full synchronization interval in milliseconds.</param>
        /// <param name="maxDegreeOfParallelism">A maximum number of concurrent tasks.</param>
        /// <param name="log4net">Log4net logger.</param>
        public VirtualEngineBase(
            string license, 
            string userFileSystemRootPath, 
            string remoteStorageRootPath, 
            string iconsFolderPath, 
            string rpcCommunicationChannelName,
            double syncIntervalMs,
            int maxDegreeOfParallelism,
            ILog log4net) 
            : base(license, userFileSystemRootPath, maxDegreeOfParallelism)
        {
            logger = new Logger("File System Engine", log4net) ?? throw new NullReferenceException(nameof(log4net));
            this.iconsFolderPath = iconsFolderPath ?? throw new NullReferenceException(nameof(iconsFolderPath));
            this.grpcServer = new GrpcServer(rpcCommunicationChannelName, this, log4net);

            // We want our file system to run regardless of any errors.
            // If any request to file system fails in user code or in Engine itself we continue processing.
            ThrowExceptions = false;

            StateChanged += Engine_StateChanged;
            Error += Engine_Error;
            Message += Engine_Message;

            //RemoteStorageMonitor = new RemoteStorageMonitor(remoteStorageRootPath, this, log4net);
            SyncService = new FullSyncService(syncIntervalMs, userFileSystemRootPath, this, log4net);
        }

        //public abstract IMapping Mapping { get; }

        public string IconsFolderPath => iconsFolderPath;

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
            catch(FileNotFoundException ex)
            {
                // Typically the file is not found in case of some temporary file that is being deleted.
                // We do not want to continue processing this file, and we do not want any exceptions in the log as this is a normal behaviour.
                LogMessage(ex.Message, userFileSystemPath, userFileSystemNewPath, operationContext);
                return true;
            }
        }

        /// <inheritdoc/>
        public override async Task StartAsync(bool processModified = true)
        {
            await base.StartAsync(processModified);
            //RemoteStorageMonitor.Start();
            //await SyncService.StartAsync();
            grpcServer.Start();
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();
            //RemoteStorageMonitor.Stop();
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
        /// Gets thumbnail.
        /// </summary>
        /// <param name="userFileSystemPath">Path in the user file system.</param>
        /// <param name="size">Thumbnail size in pixels.</param>
        /// <remarks>
        /// Throws <see cref="NotImplementedException"/> if thumbnail is not available.
        /// </remarks>
        /// <returns>
        /// Thumbnail bitmap or null if no thumbnail should be displayed in the file manager for this item.
        /// </returns>
        public abstract Task<byte[]> GetThumbnailAsync(string userFileSystemPath, uint size);

        /// <summary>
        /// Gets list of item properties.
        /// </summary>
        /// <param name="userFileSystemPath">Path in the user file system.</param>
        /// <returns>
        /// List of properties to be displayed in the file manager that correspond to the path 
        /// provided in the <paramref name="userFileSystemPath"/> parameter.
        /// </returns>
        public abstract Task<IEnumerable<FileSystemItemPropertyData>> GetItemPropertiesAsync(string userFileSystemPath);

        private bool disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //RemoteStorageMonitor.Dispose();
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
