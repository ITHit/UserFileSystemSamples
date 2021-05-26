using System.IO;
using System.Threading.Tasks;
using log4net;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;

using ITHit.FileSystem.Samples.Common.Windows;
using System;

namespace WebDAVDrive
{
    /// <inheritdoc />
    public class VirtualEngine : EngineWindows
    {
        /// <summary>
        /// Logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Monitors changes in the remote storage, notifies the client and updates the user file system.
        /// </summary>
        internal readonly RemoteStorageMonitor RemoteStorageMonitor;

        /// <summary>
        /// Monitors MS Office documents renames in the user file system.
        /// </summary>
        private readonly MsOfficeDocsMonitor msOfficeDocsMonitor;

        /// <summary>
        /// Path to the folder that custom data associated with files and folders.
        /// </summary>
        private readonly string serverDataFolderPath;

        /// <summary>
        /// Path to the icons folder.
        /// </summary>
        private readonly string iconsFolderPath;

        /// <summary>
        /// Creates a vitual file system Engine.
        /// </summary>
        /// <param name="license">A license string.</param>
        /// <param name="userFileSystemRootPath">
        /// A root folder of your user file system. 
        /// Your file system tree will be located under this folder.
        /// </param>
        /// <param name="serverDataFolderPath">Path to the folder that stores custom data associated with files and folders.</param>
        /// <param name="iconsFolderPath">Path to the icons folder.</param>
        /// <param name="log">Logger.</param>
        public VirtualEngine(string license, string userFileSystemRootPath, string serverDataFolderPath, string iconsFolderPath, ILog log) 
            : base(license, userFileSystemRootPath)
        {
            logger = new Logger("File System Engine", log) ?? throw new NullReferenceException(nameof(log));
            this.serverDataFolderPath = serverDataFolderPath ?? throw new NullReferenceException(nameof(serverDataFolderPath));
            this.iconsFolderPath = iconsFolderPath ?? throw new NullReferenceException(nameof(iconsFolderPath));

            // We want our file system to run regardless of any errors.
            // If any request to file system fails in user code or in Engine itself we continue processing.
            ThrowExceptions = false;

            StateChanged += Engine_StateChanged;
            Error += Engine_Error;
            Message += Engine_Message;

            string remoteStorageRootPath = Mapping.MapPath(userFileSystemRootPath);
            RemoteStorageMonitor = new RemoteStorageMonitor(remoteStorageRootPath, this, log);
            msOfficeDocsMonitor = new MsOfficeDocsMonitor(userFileSystemRootPath, this, log);
        }

        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(string path, FileSystemItemType itemType)
        {
            // When a file or folder is deleted, the item may be already 
            // deleted in user file system when this method is called
            // The Engine calls IFile.CloseAsync() and IFileSystemItem.DeleteCompletionAsync() methods in this case.

            // On macOS there is no access to the local file system. 
            // You should NOT try to determine item type or read local files/folders on macOS.

            if (itemType == FileSystemItemType.File)
            {
                return new VirtualFile(path, this, this);
            }
            else
            {
                return new VirtualFolder(path, this, this);
            }
        }

        /// <inheritdoc/>
        public override async Task<bool> FilterAsync(string userFileSystemPath, string userFileSystemNewPath = null)
        {
            // IsMsOfficeLocked() check is required for MS Office PowerPoint. 
            // PowerPoint does not block the file for reading when the file is opened for editing.
            // As a result the file will be sent to the remote storage during each file save operation.

            if (userFileSystemNewPath == null)
            {
                // Executed during create, update, delete, open, close.
                return MsOfficeHelper.AvoidMsOfficeSync(userFileSystemPath);
            }
            else
            {
                // Executed during rename/move operation.
                return 
                       MsOfficeHelper.IsRecycleBin(userFileSystemNewPath) // When a hydrated file is deleted, it is moved to a Recycle Bin.
                    || MsOfficeHelper.AvoidMsOfficeSync(userFileSystemNewPath);
            }
        }

        /// <inheritdoc/>
        public override async Task StartAsync()
        {
            await base.StartAsync();
            RemoteStorageMonitor.Start();
            msOfficeDocsMonitor.Start();
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();
            RemoteStorageMonitor.Stop();
            msOfficeDocsMonitor.Stop();
        }

        private void Engine_Message(IEngine sender, EngineMessageEventArgs e)
        {
            logger.LogMessage(e.Message, e.SourcePath, e.TargetPath);
        }

        private void Engine_Error(IEngine sender, EngineErrorEventArgs e)
        {
            logger.LogError(e.Message, e.SourcePath, e.TargetPath, e.Exception);
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
        /// Manages custom data associated with the item. 
        /// </summary>
        internal CustomDataManager CustomDataManager(string userFileSystemPath, ILogger logger = null)
        {
            return new CustomDataManager(userFileSystemPath, serverDataFolderPath, Path, iconsFolderPath, logger ?? this.logger);
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
