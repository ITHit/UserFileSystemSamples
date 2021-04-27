using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using log4net;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows.Syncronyzation;


namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <inheritdoc cref="IVirtualDrive"/>
    public abstract class VirtualDriveBase : IVirtualDrive, IDisposable
    {
        /// <summary>
        /// Virtual drive settings.
        /// </summary>
        public readonly Settings Settings;

        /// <summary>
        /// Current synchronization state of this virtual drive.
        /// </summary>
        public virtual SynchronizationState SyncState { get; private set; } = SynchronizationState.Disabled;

        /// <summary>
        /// Event, fired when synchronization state changes.
        /// </summary>
        public event SyncronizationEvent SyncEvent;

        /// <summary>
        /// Processes file system calls, implements on-demand folders listing 
        /// and initial on-demand file content transfer from remote storage to client.
        /// </summary>
        internal readonly VfsEngine Engine;

        /// <summary>
        /// Monitors pinned and unpinned attributes in user file system.
        /// </summary>
        private readonly UserFileSystemMonitor userFileSystemMonitor;

        /// <summary>
        /// Performs complete synchronyzation of the folders and files that are already synched to user file system.
        /// </summary>
        public readonly FullSyncService SyncService;

        /// <summary>
        /// Log4Net logger.
        /// </summary>
        private readonly ILog log;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="license">A license string.</param>
        /// <param name="userFileSystemRootPath">
        /// A root folder of your user file system. Your file system tree will be located under this folder.
        /// </param>
        /// <param name="log">Log4net logger.</param>
        /// <param name="settings">Virtual drive settings.</param>
        public VirtualDriveBase(string license, string userFileSystemRootPath, Settings settings, ILog log)
        {
            this.Settings = settings;
            this.log = log;
            Engine = new VfsEngine(license, userFileSystemRootPath, this, log);
            SyncService = new FullSyncService(settings.SyncIntervalMs, userFileSystemRootPath, this, log);
            SyncService.SyncEvent += SyncService_SyncEvent;
            userFileSystemMonitor = new UserFileSystemMonitor(userFileSystemRootPath, this, log);
        }

        private void SyncService_SyncEvent(object sender, SynchEventArgs synchEventArgs)
        {
            InvokeSyncEvent(synchEventArgs.NewState);
        }

        /// <inheritdoc/>
        public abstract Task<IVirtualFileSystemItem> GetVirtualFileSystemItemAsync(string userFileSystemPath, FileSystemItemTypeEnum itemType, ILogger logger);

        /// <summary>
        /// This is a strongly typed variant of GetVirtualFileSystemItemAsync() method for internal use.
        /// </summary>
        /// <typeparam name="T">This is either <see cref="IVirtualFile"/> or <see cref="IVirtualFolder"/>.</typeparam>
        /// <param name="userFileSystemPath">Path in user file system for which your will return a file or a folder.</param>
        /// <returns>File or folder object that corresponds to the path in user file system.</returns>
        internal async Task<T> GetItemAsync<T>(string userFileSystemPath, ILogger logger) where T : IVirtualFileSystemItem
        {
            FileSystemItemTypeEnum itemType = typeof(T).Name == nameof(IVirtualFile) ? FileSystemItemTypeEnum.File : FileSystemItemTypeEnum.Folder;

            IVirtualFileSystemItem userItem = await GetVirtualFileSystemItemAsync(userFileSystemPath, itemType, logger);
            if (userItem == null)
            {
                throw new System.IO.FileNotFoundException($"{itemType} not found.", userFileSystemPath);
            }

            if ((T)userItem == null)
            {
                throw new NotImplementedException($"{typeof(T).Name}");
            }

            return (T)userItem;
        }

        

        /// <summary>
        /// Starts processing OS file system calls, starts user file system to remote storage synchronization 
        /// and remote storage to user file system synchronization
        /// as well as starts monitoring for pinned/unpinned files.
        /// </summary>
        public virtual async Task StartAsync()
        {
            // Start processing OS file system calls.
            Engine.ChangesProcessingEnabled = true;
            await Engine.StartAsync();

            await SetEnabledAsync(true);
        }

        /// <inheritdoc/>
        public virtual async Task SetEnabledAsync(bool enabled)
        {
            if (enabled)
            {
                // Start periodical synchronyzation between client and server, 
                // in case any changes are lost because the client or the server were unavailable.
                await SyncService.StartAsync();

                // Start monitoring pinned/unpinned attributes and files/folders creation in user file system.
                await userFileSystemMonitor.StartAsync();
            }
            else
            {
                await SyncService.StopAsync();
                await userFileSystemMonitor.StopAsync();
            }
        }

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Engine.Dispose();
                    SyncService.Dispose();
                    userFileSystemMonitor.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~UserEngine()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void InvokeSyncEvent(SynchronizationState state)
        {
            SyncState = state;
            if (SyncEvent != null)
            {
                SyncEvent.Invoke(this, new SynchEventArgs(state));
            }
        }

        /// <inheritdoc/>
        public IServerNotifications ServerNotifications(string userFileSystemPath, ILogger logger)
        {
            return GetUserFileSystemRawItem(userFileSystemPath, logger);
        }

        /// <inheritdoc/>
        public IClientNotifications ClientNotifications(string userFileSystemPath, ILogger logger)
        {
            FileSystemItemTypeEnum itemType = FsPath.GetItemType(userFileSystemPath);
            return GetRemoteStorageRawItem(userFileSystemPath, itemType, logger);
        }

        internal LockManager LockManager(string userFileSystemPath, ILogger logger)
        {
            return new LockManager(userFileSystemPath, Settings.ServerDataFolderPath, Engine.Path, logger);
        }

        /// <summary>
        /// Provides methods for reading and writing eTags.
        /// </summary>
        /// <param name="userFileSystemPath">User file system item path.</param>
        /// <param name="logger">Logger.</param>
        /// <returns></returns>
        public ETagManager GetETagManager(string userFileSystemPath, ILogger logger = null)
        {
            logger ??= new Logger("Virtual Drive", log);
            return new ETagManager(userFileSystemPath, Settings.ServerDataFolderPath, Engine.Path, logger);
        }

        internal IRemoteStorageRawItem GetRemoteStorageRawItem(string userFileSystemPath, FileSystemItemTypeEnum itemType, ILogger logger)
        {
            if (itemType == FileSystemItemTypeEnum.File)
            {
                return new RemoteStorageRawItem<IVirtualFile>(userFileSystemPath, this, logger);
            }
            else
            {
                return new RemoteStorageRawItem<IVirtualFolder>(userFileSystemPath, this, logger);
            }
        }

        internal UserFileSystemRawItem GetUserFileSystemRawItem(string userFileSystemPath, ILogger logger)
        {
            return new UserFileSystemRawItem(userFileSystemPath, this, logger);
        }
    }
}
