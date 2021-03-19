using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Provider;

namespace ITHit.FileSystem.Samples.Common.Syncronyzation
{
    /// <summary>
    /// Performs a full synchronization between the user file system and the remote storage, recursively processing all folders.
    /// </summary>
    /// <remarks>
    /// This is a simple full synchronyzation example. 
    /// 
    /// You can use this class in your project out of the box or replace it with a more advanced algorithm.
    /// </remarks>
    public class FullSyncService : Logger, IDisposable
    {
        /// <summary>
        /// FullSyncService states.
        /// </summary>
        public enum SynchronizationState 
        {
            Started,
            Stopped,
            Idle
        }
        /// <summary>
        /// Virtual drive.
        /// </summary>
        private VirtualDriveBase virtualDrive;

        /// <summary>
        /// Timer to start synchronization.
        /// </summary>
        private System.Timers.Timer timer = null;


        public class SynchEventArgs : EventArgs
        {
            public SynchronizationState state;

            public SynchEventArgs(SynchronizationState state) 
            {
                this.state = state;
            }
        }

        public delegate void SyncEvent(object sender, SynchEventArgs synchEventArgs);
        public event SyncEvent syncEvent;

        /// <summary>
        /// User file system path.
        /// </summary>
        private string userFileSystemRootPath;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="syncIntervalMs">Synchronization interval in milliseconds.</param>
        /// <param name="userFileSystemRootPath">User file system root path.</param>
        /// <param name="log">Logger.</param>
        internal FullSyncService(double syncIntervalMs, string userFileSystemRootPath, VirtualDriveBase virtualDrive, ILog log) : base("Sync Service", log)
        {
            timer = new System.Timers.Timer(syncIntervalMs);
            timer.Elapsed += Timer_ElapsedAsync;
            this.userFileSystemRootPath = userFileSystemRootPath;
            this.virtualDrive = virtualDrive;
        }

        /// <summary>
        /// Starts synchronization.
        /// </summary>
        public async Task StartAsync()
        {
            // Do not start next synchronyzation automatically, wait until previous synchronyzation completed.
            timer.AutoReset = false; 
            timer.Start();
            LogMessage($"Started");
        }

        /// <summary>
        /// Stops synchronization.
        /// </summary>
        public async Task StopAsync() 
        {
            timer.Stop();
            LogMessage($"Stopped");
        }

        private void InvokeSyncEvent(SynchronizationState state) 
        {
            if (syncEvent != null)
            {
                syncEvent.Invoke(this, new SynchEventArgs(state));
            }
        }

        private async void Timer_ElapsedAsync(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                InvokeSyncEvent(SynchronizationState.Started);
                // UFS -> RS. Recursivery synchronize all moved files and folders present in the user file system. Restore OriginalPath.
                await new ClientToServerSync(virtualDrive, Log).SyncronizeMovedAsync(userFileSystemRootPath);

                // UFS -> RS. Recursivery synchronize all updated/created/deleted file and folders present in the user file system.
                await new ClientToServerSync(virtualDrive, Log).SyncronizeFolderAsync(userFileSystemRootPath);

                // UFS <- RS. Recursivery synchronize all updated/created/deleted file and folders present in the user file system.
                await new ServerToClientSync(virtualDrive, Log).SyncronizeFolderAsync(userFileSystemRootPath);
                InvokeSyncEvent(SynchronizationState.Stopped);
            }
            catch(Exception ex)
            {
                LogError(null, null, null, ex);
            }
            finally
            {
                // Wait and than start synchronyzation again.
                timer.Start();
            }
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
                    timer.Stop();
                    timer.Dispose();
                    LogMessage($"Disposed");
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~SyncService()
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
