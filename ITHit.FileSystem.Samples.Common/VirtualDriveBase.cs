using ITHit.FileSystem;
using log4net;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ITHit.FileSystem.Samples.Common.Syncronyzation;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Represents a virtual drive. Processes OS file system calls, 
    /// synchronizes user file system to remote storage and back, 
    /// monitors files pinning and unpinning.
    /// </summary>
    /// <remarks>
    /// This class calls <see cref="IUserFile"/> and <see cref="IUserFolder"/> interfaces.
    /// </remarks>
    public abstract class VirtualDriveBase : IDisposable
    {
        /// <summary>
        /// Processes file system calls, implements on-demand folders listing 
        /// and initial on-demand file content transfer from remote storage to client.
        /// </summary>
        private VfsEngine engine;

        /// <summary>
        /// Monitors pinned and unpinned attributes in user file system.
        /// </summary>
        private UserFileSystemMonitor userFileSystemMonitor;

        /// <summary>
        /// Performs complete synchronyzation of the folders and files that are already synched to user file system.
        /// </summary>
        public FullSyncService SyncService;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="license">A license string.</param>
        /// <param name="userFileSystemRootPath">
        /// A root folder of your user file system. Your file system tree will be located under this folder.
        /// </param>
        /// <param name="log">Log4net logger.</param>
        /// <param name="syncIntervalMs">Full synchronization interval in milliseconds.</param>
        public VirtualDriveBase(string license, string userFileSystemRootPath, ILog log, double syncIntervalMs)
        {
            engine = new VfsEngine(license, userFileSystemRootPath, this, log);
            SyncService = new FullSyncService(syncIntervalMs, userFileSystemRootPath, this, log);
            userFileSystemMonitor = new UserFileSystemMonitor(userFileSystemRootPath, this, log);
        }

        /// <summary>
        /// Gets file or folder object corresponding to path in user file system.
        /// </summary>
        /// <param name="userFileSystemPath">
        /// Path in user file system for which your will return a file or a folder.
        /// </param>
        /// <remarks>
        /// <para>
        /// This is a factory method that returns files and folders in your user file system.
        /// In your implementation you will return file or folder object that corresponds to provided <paramref name="userFileSystemPath"/> parameter.
        /// Your file must implement <see cref="IUserFile"/> interface. Your folder must implement <see cref="IUserFolder"/> interface.
        /// </para>
        /// <para>
        /// The <see cref="VirtualDriveBase"/> will then call <see cref="IUserFile"/> and <see cref="IUserFolder"/> methods to get the 
        /// required information and pass it to the platform.
        /// </para>
        /// <para>
        /// Note that this method may be called for files that does not exist in the user file system, 
        /// for example when a file handle is closed after the file has been deleted.
        /// </para>
        /// </remarks>
        /// <returns>File or folder object that corresponds to the path in user file system.</returns>
        public abstract Task<IUserFileSystemItem> GetUserFileSystemItemAsync(string userFileSystemPath);

        /// <summary>
        /// This is a strongly typed variant of GetUserFileSystemItemAsync() method for internal use.
        /// </summary>
        /// <typeparam name="T">This is either <see cref="IUserFile"/> or <see cref="IUserFolder"/> or <see cref="IUserFileSystemItem"/>.</typeparam>
        /// <param name="userFileSystemPath">Path in user file system for which your will return a file or a folder.</param>
        /// <returns>File or folder object that corresponds to the path in user file system.</returns>
        internal async Task<T> GetItemAsync<T>(string userFileSystemPath) where T : class, IUserFileSystemItem
        {
            IUserFileSystemItem userItem = await GetUserFileSystemItemAsync(userFileSystemPath);
            if (userItem as T == null)
            {
                throw new NotImplementedException($"{typeof(T).Name}");
            }

            return userItem as T;
        }

        /// <summary>
        /// Starts processing OS file system calls, starts user file system to remote storage synchronization 
        /// and remote storage to user file system synchronization
        /// as well as starts monitoring for pinned/unpinned files.
        /// </summary>
        public async Task StartAsync()
        {
            // Start processing OS file system calls.
            engine.ChangesProcessingEnabled = true;
            await engine.StartAsync();

            // Start periodical synchronyzation between client and server, 
            // in case any changes are lost because the client or the server were unavailable.
            await SyncService.StartAsync();

            // Start monitoring pinned/unpinned attributes and files/folders creation in user file system.
            await userFileSystemMonitor.StartAsync();
        }        

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    engine.Dispose();
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
    }
}
