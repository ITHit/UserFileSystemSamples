using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Manages lock-sync, lock-info and lock mode files that correspond with the file in the user file system. 
    /// </summary>
    /// <remarks>
    /// <para>
    /// When a file must be sent from user file system to remote storage, create a lock-sync using 
    /// <see cref="LockAsync"/> method to prevent concurrent threads sending the file to 
    /// the remote storage, so only one thread can start the synchronization.
    /// </para>
    /// <para>
    /// The lock info must be stored outside of CustomData, because the file in user file system 
    /// is renamed and deleted during MS Office transactional save operation. 
    /// The lock must remain regardless of the transactional save.
    /// </para>
    /// </remarks>
    internal class LockManager
    {
        /// <summary>
        /// Path in user file system with which this lock corresponds.
        /// </summary>
        private readonly string userFileSystemPath;

        /// <summary>
        /// Path to the file that serves as lock-sync.
        /// </summary>
        private readonly string lockSyncFilePath;

        /// <summary>
        /// Path to the file that contains the lock mode.
        /// </summary>
        private readonly string lockModeFilePath;

        /// <summary>
        /// Path to the file that contains the lock-token and other lock info.
        /// </summary>
        private readonly string lockInfoFilePath;

        /// <summary>
        /// Logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        internal LockManager(string userFileSystemPath, string serverDataFolderPath, string userFileSystemRootPath, ILogger logger)
        {
            this.userFileSystemPath = userFileSystemPath;
            this.logger = logger;

            // Get path relative to the virtual root.
            string relativePath = userFileSystemPath.TrimEnd(Path.DirectorySeparatorChar).Substring(
                userFileSystemRootPath.TrimEnd(Path.DirectorySeparatorChar).Length);

            string dataFile = $"{serverDataFolderPath.TrimEnd(Path.DirectorySeparatorChar)}{relativePath}";

            lockSyncFilePath = $"{dataFile}.locksync";

            lockModeFilePath = $"{dataFile}.lockmode";

            lockInfoFilePath = $"{dataFile}.lockinfo";
        }

        /// <summary>
        /// Gets existing file lock or creates a new lock. 
        /// </summary>
        /// <exception cref="ClientLockFailedException">
        /// Thrown when a file can not be locked. For example when a lock-token file is blocked 
        /// from another thread, during update, lock and unlock operations.
        /// </exception>
        /// <returns>Lock sync.</returns>
        internal async Task<LockSync> LockAsync()
        {
            return new LockSync(lockSyncFilePath, userFileSystemPath, logger);
        }

        /// <summary>
        /// Sets lock mode associated with the file or folder.
        /// </summary>
        /// <param name="lockMode">Lock mode.</param>
        internal async Task SetLockModeAsync(LockMode lockMode)
        {
            if (lockMode == LockMode.None)
            {
                File.Delete(lockModeFilePath);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(lockModeFilePath));
            await using (FileStream fileStream = File.Open(lockModeFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Delete))
            {
                fileStream.WriteByte((byte)lockMode);
            }
        }

        /// <summary>
        /// Gets lock mode associated with a file or folder.
        /// </summary>
        /// <returns>Lock mode or <see cref="LockMode.None"/> if the file is not locked.</returns>
        internal async Task<LockMode> GetLockModeAsync()
        {
            if(!File.Exists(lockModeFilePath))
            {
                return LockMode.None;
            }

            await using (FileStream fileStream = File.Open(lockModeFilePath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read))
            {
                return (LockMode)fileStream.ReadByte();
            }
        }

        /// <summary>
        /// Returns true if the file or folder is locked. 
        /// </summary>
        internal async Task<bool> IsLockedAsync()
        {
            return File.Exists(lockInfoFilePath);
        }

        /// <summary>
        /// Gets lock info.
        /// </summary>
        internal async Task<ServerLockInfo> GetLockInfoAsync()
        {
            if(!File.Exists(lockInfoFilePath))
            {
                return null;
            }
            await using (FileStream lockTokenFileStream = File.Open(lockInfoFilePath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read))
            {
                lockTokenFileStream.Seek(0, SeekOrigin.Begin);
                return await JsonSerializer.DeserializeAsync<ServerLockInfo>(lockTokenFileStream);
            }
        }

        /// <summary>
        /// Sets lock info.
        /// </summary>
        /// <param name="lockInfo">Lock info.</param>
        internal async Task SetLockInfoAsync(ServerLockInfo lockInfo)
        {
            await using (FileStream lockTokenFileStream = File.Open(lockInfoFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Delete))
            {
                lockTokenFileStream.Seek(0, SeekOrigin.Begin);
                await JsonSerializer.SerializeAsync(lockTokenFileStream, lockInfo);
                //lockTokenFileStream.SetLength(lockTokenFileStream.Position);
            }
        }

        /// <summary>
        /// Deletes lock-token and lock-mode files.
        /// </summary>
        internal async Task DeleteLockAsync()
        {
            try
            {
                File.Delete(lockModeFilePath);
            }
            catch (Exception ex)
            {
                logger.LogError("Failed to delete lock-mode file.", userFileSystemPath, null, ex);
            }

            try
            {
                File.Delete(lockInfoFilePath);
            }
            catch (Exception ex)
            {
                logger.LogError("Failed to delete lock-token file.", userFileSystemPath, null, ex);
            }
        }

    }
}
