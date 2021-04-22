using ITHit.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.System;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Protects file from concurrent updates during user file system to remote storage synchronization.
    /// </summary>
    internal class LockSync : IDisposable
    {
        /// <summary>
        /// Path in user file system with which this lock corresponds.
        /// </summary>
        private readonly string userFileSystemPath;

        /// <summary>
        /// Open lock-info file stream.
        /// </summary>
        private readonly FileStream lockTokenFileStream = null;

        /// <summary>
        /// Logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">Lock-sync file path.</param>
        internal LockSync(string lockSyncFilePath, string userFileSystemPath, ILogger logger)
        {
            this.logger = logger;

            // Create lock-token file or open existing lock-token file.
            try
            {
                // Blocks the file for reading and writing, 
                // so other threads can not start file update, lock and unlock file until this lock request is completed
                // and lock-toked is saved in a lock-token file.
                // Lock-token and lock-mode files can still be updated independently, we do not want to block their reading/writing.
                lockTokenFileStream = File.Open(lockSyncFilePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Delete);
            }
            catch (IOException ex)
            {
                // Possibly blocked in another thread. This is a normal behaviour.
                throw new ClientLockFailedException("Can't open or create lock-sync file.", ex);
            }

            logger.LogMessage("Lock-sync created succesefully", userFileSystemPath);
        }

        /*
        /// <summary>
        /// Gets existing file lock or creates a new lock. 
        /// </summary>
        /// <param name="lockFileOpenMode">
        /// Indicates if a new lock should be created or existing lock file to be opened.
        /// Allowed options are <see cref="FileMode.OpenOrCreate"/>, <see cref="FileMode.Open"/> and <see cref="FileMode.CreateNew"/>.
        /// </param>
        /// <exception cref="ClientLockFailedException">
        /// Thrown when a file can not be locked. For example when a lock-token file is blocked 
        /// from another thread, during update, lock and unlock operations.
        /// </exception>
        /// <returns>File lock info.</returns>
        internal static async Task<LockSync> CreateAsync(string lockFilePath, FileMode lockFileOpenMode, ILogger logger)
        {
            if ((lockFileOpenMode != FileMode.OpenOrCreate)
                && (lockFileOpenMode != FileMode.Open)
                && (lockFileOpenMode != FileMode.CreateNew))
            {
                throw new ArgumentOutOfRangeException(nameof(lockFileOpenMode), $"Must be {FileMode.OpenOrCreate} or {FileMode.Open} or {FileMode.CreateNew}");
            }

            // Create lock-token file or open existing lock-token file.
            FileStream lockTokenFileStream = null;
            try
            {
                // Blocks the file for reading and writing, 
                // so other threads can not start file update, lock and unlock file until this lock request is completed
                // and lock-toked is saved in a lock-token file.
                // Lock-token and lock-mode files can still be updated independently, we do not want to block their reading/writing.
                lockTokenFileStream = File.Open(lockFilePath, lockFileOpenMode, FileAccess.ReadWrite, FileShare.Delete);
            }
            catch (IOException ex)
            {
                // Possibly blocked in another thread. This is a normal behaviour.
                throw new ClientLockFailedException("Can't open or create lock-sync file.", ex);
            }

            return new LockSync(lockTokenFileStream, logger);
            
            LockSync lockInfo = null;
            try
            {
                lockInfo = new LockSync(lockTokenFileStream, logger);
            }
            catch (Exception ex)
            {
                // Something went wrong, cleanup.
                try
                {
                    lockInfo.Dispose();
                    File.Delete(lockFilePath);
                }
                catch { }
                throw;
            }

            return lockInfo;
            
        }
        */
        /*
        /// <summary>
        /// Returns true if the lock was created and contains no data. 
        /// Returns false if the lock contains information about the lock.
        /// </summary>
        internal bool IsNew()
        {
            return lockTokenFileStream.Length == 0;
        }

        internal async Task SetLockInfoAsync(ServerLockInfo lockInfo)
        {
            lockTokenFileStream.Seek(0, SeekOrigin.Begin);
            await JsonSerializer.SerializeAsync(lockTokenFileStream, lockInfo);
            lockTokenFileStream.SetLength(lockTokenFileStream.Position);
        }

        internal async Task<ServerLockInfo> GetLockInfoAsync()
        {
            lockTokenFileStream.Seek(0, SeekOrigin.Begin);
            return await JsonSerializer.DeserializeAsync<ServerLockInfo>(lockTokenFileStream);
        }
        */
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).

                    // Just close the stream, do not delete lock-mode and lock-token files.
                    if (lockTokenFileStream != null)
                    {
                        lockTokenFileStream.Close();
                        File.Delete(lockTokenFileStream.Name);
                        logger.LogMessage("Lock-sync deleted succesefully", userFileSystemPath);
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Lock2()
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
