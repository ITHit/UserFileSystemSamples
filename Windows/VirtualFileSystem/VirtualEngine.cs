using System.IO;
using System.Threading.Tasks;
using log4net;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;

using ITHit.FileSystem.Samples.Common.Windows;


namespace VirtualFileSystem
{
    
    /// <inheritdoc />
    public class VirtualEngine : EngineWindows
    {
        /// <summary>
        /// Logger.
        /// </summary>
        private ILogger logger;

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
        /// <param name="log">Logger.</param>
        public VirtualEngine(string license, string userFileSystemRootPath, string remoteStorageRootPath, int maxDegreeOfParallelism, ILog log) : 
            base(license, userFileSystemRootPath, maxDegreeOfParallelism)
        {
            logger = new Logger("File System Engine", log);

            // We want our file system to run regardless of any errors.
            // If any request to file system fails in user code or in Engine itself we continue processing.
            ThrowExceptions = false;

            StateChanged += Engine_StateChanged;
            Error += Engine_Error;
            Message += Engine_Message;

            RemoteStorageMonitor = new RemoteStorageMonitor(remoteStorageRootPath, this, log);
        }
        
        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(string userFileSystemPath, FileSystemItemType itemType, byte[] remoteStorageItemId = null)
        {
            if (itemType == FileSystemItemType.File)
            {
                return new VirtualFile(userFileSystemPath, remoteStorageItemId, this);
            }
            else
            {
                return new VirtualFolder(userFileSystemPath, remoteStorageItemId, this);
            }
        }

        /// <inheritdoc/>
        public override async Task StartAsync(bool processModified = true)
        {
            await base.StartAsync();
            RemoteStorageMonitor.Start();
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();
            RemoteStorageMonitor.Stop();
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
