using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using ITHit.FileSystem.Windows;


namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Manages lock-info and lock mode files that correspond with the file in the user file system. 
    /// </summary>
    /// <remarks>
    /// <para>
    /// The lock info must be stored outside of <see cref="IFileSystemItemMetadata.CustomData"/>, because the file in user file system 
    /// is renamed and deleted during MS Office transactional save operation. 
    /// The lock must remain regardless of the transactional save.
    /// </para>
    /// </remarks>
    public class LockManager
    {
        /// <summary>
        /// Path in user file system with which this lock corresponds.
        /// </summary>
        private readonly string userFileSystemPath;

        /// <summary>
        /// Path to the folder that stores custom data associated with files and folders.
        /// </summary>
        private readonly string serverDataFolderPath;

        /// <summary>
        /// Virtual file system root path.
        /// </summary>
        private readonly string userFileSystemRootPath;

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

        private const string lockModeExt = ".lockmode";

        private const string lockInfoExt = ".lockinfo";

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        public LockManager(string userFileSystemPath, string serverDataFolderPath, string userFileSystemRootPath, ILogger logger)
        {
            this.userFileSystemPath = userFileSystemPath ?? throw new NullReferenceException(nameof(userFileSystemPath));
            this.serverDataFolderPath = serverDataFolderPath ?? throw new NullReferenceException(nameof(serverDataFolderPath));
            this.userFileSystemRootPath = userFileSystemRootPath ?? throw new NullReferenceException(nameof(userFileSystemRootPath));
            this.logger = logger ?? throw new NullReferenceException(nameof(logger));

            // Get path relative to the virtual root.
            string dataFile = GetLockFilePath(userFileSystemPath);
            lockModeFilePath = $"{dataFile}{lockModeExt}";
            lockInfoFilePath = $"{dataFile}{lockInfoExt}";
        }

        /// <summary>
        /// Sets lock mode associated with the file or folder.
        /// </summary>
        /// <param name="lockMode">Lock mode.</param>
        public async Task SetLockModeAsync(LockMode lockMode)
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
        public async Task<LockMode> GetLockModeAsync()
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
        public async Task<bool> IsLockedAsync()
        {
            return File.Exists(lockInfoFilePath);
        }

        /// <summary>
        /// Gets lock info or null if the item is not locked.
        /// </summary>
        public async Task<ServerLockInfo> GetLockInfoAsync()
        {
            if(!File.Exists(lockInfoFilePath))
            {
                return null;
            }
            await using (FileStream stream = File.Open(lockInfoFilePath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read))
            {
                stream.Seek(0, SeekOrigin.Begin);
                return await JsonSerializer.DeserializeAsync<ServerLockInfo>(stream);
            }
        }

        /// <summary>
        /// Sets lock info.
        /// </summary>
        /// <param name="lockInfo">Lock info.</param>
        public async Task SetLockInfoAsync(ServerLockInfo lockInfo)
        {
            await using (FileStream stream = File.Open(lockInfoFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Delete))
            {
                stream.Seek(0, SeekOrigin.Begin);
                await JsonSerializer.SerializeAsync(stream, lockInfo);
                stream.SetLength(stream.Position);
            }
        }

        /// <summary>
        /// Deletes lock-token and lock-mode files.
        /// </summary>
        public void DeleteLock()
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

        /// <summary>
        /// Gets lock file and lock mode files path (without extension).
        /// </summary>
        /// <param name="userFileSystemPath">Path of the file in user file system to get the path for.</param>
        private string GetLockFilePath(string userFileSystemPath)
        {
            // Get path relative to the virtual root.
            string relativePath = userFileSystemPath.TrimEnd(Path.DirectorySeparatorChar).Substring(
                userFileSystemRootPath.TrimEnd(Path.DirectorySeparatorChar).Length);

            return $"{serverDataFolderPath.TrimEnd(Path.DirectorySeparatorChar)}{relativePath}";
        }


        /// <summary>
        /// Moves custom columns to a new location.
        /// </summary>
        /// <param name="userFileSystemNewPath">Path of the file in the user file system to move custom columns to.</param>
        internal async Task MoveToAsync(string userFileSystemNewPath)
        {
            // Move custom columns file.
            string lockTargetPath = GetLockFilePath(userFileSystemNewPath);
            string lockInfoFileTargetPath = $"{lockTargetPath}{lockModeExt}";
            string lockModeFileTargetPath = $"{lockTargetPath}{lockInfoExt}";

            // Ensure the target directory exisit, in case we are moving into empty folder or which is offline.
            new FileInfo(lockInfoFileTargetPath).Directory.Create();
            if (File.Exists(lockInfoFilePath))
            {
                File.Move(lockInfoFilePath, lockInfoFileTargetPath);
            }
            if (File.Exists(lockModeFilePath))
            {
                File.Move(lockModeFilePath, lockModeFileTargetPath);
            }

            // If this is a folder, move all data in this folder.
            string lockSourceFolderPath = GetLockFilePath(userFileSystemPath);
            if (Directory.Exists(lockSourceFolderPath))
            {
                Directory.Move(lockSourceFolderPath, lockTargetPath);
            }
        }
    }
}
