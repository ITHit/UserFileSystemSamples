using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Provider;
using Windows.System.Update;
using log4net;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;

namespace WebDAVDrive
{
    /// <summary>
    /// Monitors changes in the remote storage, notifies the client and updates the user file system.
    /// If any file or folder is modified, created, delated, renamed or attributes changed in the remote storage, 
    /// triggers an event with information about changes being made.
    /// </summary>
    internal class RemoteStorageMonitor : Logger, IDisposable
    {
        /// <summary>
        /// Remote storage path. Folder to monitor changes in.
        /// </summary>
        private string remoteStorageRootPath;

        /// <summary>
        /// Virtul drive instance. This class will call <see cref="Engine"/> methods 
        /// to update user file system when any data is changed in the remote storage.
        /// </summary>
        private readonly VirtualEngine engine;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="remoteStorageRootPath">Remote storage path. Folder that contains source files to monitor changes.</param>
        /// <param name="engine">Engine to send notifications about changes in the remote storage.</param>
        /// <param name="log">Logger.</param>
        internal RemoteStorageMonitor(string remoteStorageRootPath, VirtualEngine engine, ILog log) : base("Remote Storage Monitor", log)
        {
            this.remoteStorageRootPath = remoteStorageRootPath;
            this.engine = engine;
        }

        /// <summary>
        /// Starts monitoring changes in the remote storage.
        /// </summary>
        internal void Start()
        {

        }

        /// <summary>
        /// Stops monitoring changes in the remote storage.
        /// </summary>
        internal void Stop()
        {

        }

        private void Error(object sender, ErrorEventArgs e)
        {
            LogError(null, null, null, e.GetException());
        }


        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    
                    //LogMessage($"Disposed");
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ServerChangesMonitor()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
    }
}
