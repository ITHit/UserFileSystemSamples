using ITHit.FileSystem;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.System;

namespace VirtualFileSystem
{
    /// <summary>
    /// Represents file lock.
    /// </summary>
    /// <remarks>
    /// The lock must be stored outside of CustomData, because the file in user file system 
    /// is renamed and deleted during MS Office transactional save operation. 
    /// The lock must remain regardless of the transactional save.
    /// 
    /// The lock-mode file is stored separately from lock-token file because we must be able to read/write 
    /// the lock-mode while the lock-token file is blocked during file update, lock and unlock operations.
    /// </remarks>
    internal class Lock : IDisposable
    {
        /// <summary>
        /// Open lock-token file stream. Corresponds with <see cref="userFileSystemPath"/>.
        /// </summary>
        private FileStream lockTokenFileStream = null;

        /// <summary>
        /// Path in user file system to which the <see cref="lockTokenFileStream"/> corresponds.
        /// </summary>
        private string userFileSystemPath = null;

        /// <summary>
        /// Logger.
        /// </summary>
        private ILogger logger = null;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">Path to the item in user file system to which this lock is applied.</param>
        /// <param name="lockTokenFileStream">Open stream to lock-token file.</param>
        /// <param name="logger">Logger.</param>
        private Lock(string userFileSystemPath, FileStream lockTokenFileStream, ILogger logger)
        {
            this.userFileSystemPath = userFileSystemPath;
            this.lockTokenFileStream = lockTokenFileStream;
            this.logger = logger;
        }

        /// <summary>
        /// Sets lock mode associated with the file or folder.
        /// </summary>
        /// <param name="userFileSystemPath">Path in the user file system.</param>
        /// <param name="lockMode">Lock mode.</param>
        internal static async Task SetLockModeAsync(string userFileSystemPath, LockMode lockMode)
        {
            if (lockMode == LockMode.None)
            {
                File.Delete(GetLockModeFilePath(userFileSystemPath));
                return;
            }

            string lockModeFilePath = GetLockModeFilePath(userFileSystemPath);
            Directory.CreateDirectory(Path.GetDirectoryName(lockModeFilePath));
            await using (FileStream fileStream = File.Open(lockModeFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Delete))
            {
                fileStream.WriteByte((byte)lockMode);
            }
        }

        /// <summary>
        /// Gets lock mode associated with a file or folder.
        /// </summary>
        /// <param name="userFileSystemPath">Path in the user file system.</param>
        /// <returns>Lock mode or <see cref="LockMode.None"/> if the file is not locked.</returns>
        internal static async Task<LockMode> GetLockModeAsync(string userFileSystemPath)
        {
            string lockModeFilePath = GetLockModeFilePath(userFileSystemPath);
            try
            {
                await using (FileStream fileStream = File.Open(lockModeFilePath, FileMode.Open, FileAccess.Read, FileShare.Delete))
                {
                    return (LockMode)fileStream.ReadByte();
                }
            }
            catch (FileNotFoundException)
            {
                return LockMode.None;
            }
        }

        /// <summary>
        /// Returns true if the file or folder is locked. Returns true if the lock, update or unlock 
        /// operation started but have not completed yet.
        /// </summary>
        /// <param name="userFileSystemPath">Path in the user file system.</param>
        /// <returns>True if the file is locked, false otherwise.</returns>
        internal static async Task<bool> IsLockedAsync(string userFileSystemPath)
        {
            string lockModeFilePath = GetLockModeFilePath(userFileSystemPath);
            return File.Exists(lockModeFilePath);
        }

        /// <summary>
        /// Gets existing file lock or creates a new lock. 
        /// </summary>
        /// <param name="userFileSystemPath">Path to the item in user file system to be locked.</param>
        /// <param name="lockFileOpenMode">
        /// Indicates if a new lock should be created or existing lock file to be opened.
        /// Allowed options are <see cref="FileMode.OpenOrCreate"/>, <see cref="FileMode.Open"/> and <see cref="FileMode.CreateNew"/>.
        /// </param>
        /// <param name="lockMode">
        /// Indicates automatic or manual lock. Saved only for new files, ignored when existing lock is opened.
        /// </param>
        /// <exception cref="ClientLockFailedException">
        /// Thrown when a file can not be locked. For example when a lock-token file is blocked 
        /// from another thread, during update, lock and unlock operations.
        /// </exception>
        /// <returns>File lock.</returns>
        internal static async Task<Lock> LockAsync(string userFileSystemPath, FileMode lockFileOpenMode, LockMode lockMode, ILogger logger)
        {
            if ((lockFileOpenMode != FileMode.OpenOrCreate) 
                && (lockFileOpenMode != FileMode.Open)
                && (lockFileOpenMode != FileMode.CreateNew))
            {
                throw new ArgumentOutOfRangeException("openMode", $"Must be {FileMode.OpenOrCreate} or {FileMode.Open} or {FileMode.CreateNew}");
            }

            string lockFilePath = GetLockTokenFilePath(userFileSystemPath);

            // Create lock-token file or open existing lock-token file.
            FileStream lockTokenFileStream = null;
            try
            {
                // Blocks lock-token file for reading and writing, 
                // so other threads can not start file update, lock and unlock file until this lock request is completed
                // and lock-toked is saved in a lock-token file.
                // Lock-mode file can still be updated directly, we do not want to block from changes.
                lockTokenFileStream = File.Open(lockFilePath, lockFileOpenMode, FileAccess.ReadWrite, FileShare.Delete);
            }
            catch (IOException ex)
            {
                // Possibly blocked in another thread. This is a normal behaviour.
                throw new ClientLockFailedException("Can't open or create lock-token file.", ex);
            }

            // Save lock mode for new locks.
            Lock lockInfo = null;
            try
            {
                lockInfo = new Lock(userFileSystemPath, lockTokenFileStream, logger);
                if (lockInfo.IsNew())
                {
                    await SetLockModeAsync(userFileSystemPath, lockMode);
                }
            }
            catch(Exception ex)
            {
                // Something went wrong, cleanup.
                try
                {
                    lockInfo.Unlock();
                    lockInfo.Dispose();
                }
                catch { }
                throw new ClientLockFailedException("Can't set lock-mode.", ex);
            }

            return lockInfo;
        }

        /// <summary>
        /// Returns true if the lock was created and contains no data. 
        /// Returns false if the lock contains information about the lock.
        /// </summary>
        internal bool IsNew()
        {
            return lockTokenFileStream.Length == 0;
        }

        internal async Task SetLockInfoAsync(LockInfo lockInfo)
        {
            lockTokenFileStream.Seek(0, SeekOrigin.Begin);
            await JsonSerializer.SerializeAsync(lockTokenFileStream, lockInfo);
            lockTokenFileStream.SetLength(lockTokenFileStream.Position);
        }

        internal async Task<LockInfo> GetLockInfoAsync()
        {
            lockTokenFileStream.Seek(0, SeekOrigin.Begin);
            return await JsonSerializer.DeserializeAsync<LockInfo>(lockTokenFileStream);
        }

        /// <summary>
        /// Gets path to the file in which lock mode is stored based on the provided user file system path.
        /// </summary>
        /// <param name="userFileSystemPath">Path to the file or folder to get the lock mode file path.</param>
        /// <returns>Path to the file that contains the lock mode.</returns>
        private static string GetLockModeFilePath(string userFileSystemPath)
        {
            // Get path relative to the virtual root.
            string relativePath = Path.TrimEndingDirectorySeparator(userFileSystemPath).Substring(
                Path.TrimEndingDirectorySeparator(Program.Settings.UserFileSystemRootPath).Length);

            string path = $"{Path.TrimEndingDirectorySeparator(Program.Settings.ServerDataFolderPath)}{relativePath}.lockmode";
            return path;
        }

        /// <summary>
        /// Gets path to the file in which lock token is stored based on the provided user file system path.
        /// </summary>
        /// <param name="userFileSystemPath">Path to the file or folder to get the lock token file path.</param>
        /// <returns>Path to the file that contains the lock token.</returns>
        private static string GetLockTokenFilePath(string userFileSystemPath)
        {
            // Get path relative to the virtual root.
            string relativePath = Path.TrimEndingDirectorySeparator(userFileSystemPath).Substring(
                Path.TrimEndingDirectorySeparator(Program.Settings.UserFileSystemRootPath).Length);

            string path = $"{Path.TrimEndingDirectorySeparator(Program.Settings.ServerDataFolderPath)}{relativePath}.locktoken";
            return path;
        }

        /// <summary>
        /// deletes lock-token and lock-mode files.
        /// </summary>
        internal void Unlock()
        {
            try
            {
                string lockModeFilePath = GetLockModeFilePath(userFileSystemPath);
                File.Delete(lockModeFilePath);
            }
            catch(Exception ex)
            {
                logger.LogError("Failed to delete lock-mode file.", userFileSystemPath, null, ex);
            }

            try
            {
                File.Delete(lockTokenFileStream.Name);
                lockTokenFileStream = null;
            }
            catch (Exception ex)
            {
                logger.LogError("Failed to delete lock-token file.", userFileSystemPath, null, ex);
            }
        }

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
