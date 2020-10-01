using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VirtualFileSystem.Syncronyzation;
using Windows.Storage;
using Windows.Storage.Provider;

namespace VirtualFileSystem
{
    ///<inheritdoc>
    internal abstract class VfsFileSystemItem : IFileSystemItem
    {
        /// <summary>
        /// File or folder path in user file system.
        /// </summary>
        protected readonly string FullPath;

        /// <summary>
        /// Logger.
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// User file system Engine.
        /// </summary>
        protected readonly VfsEngine Engine;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="path">File or folder path in user file system.</param>
        /// <param name="logger">Logger.</param>
        public VfsFileSystemItem(string path, ILogger logger, VfsEngine engine)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            if(logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            FullPath = path;
            Logger = logger;
            Engine = engine;
        }


        ///<inheritdoc>
        public async Task MoveToAsync(string userFileSystemNewPath, IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            string userFileSystemOldPath = this.FullPath;
            LogMessage("IFileSystemItem.MoveToAsync()", userFileSystemOldPath, userFileSystemNewPath);

            // Save ETag in MS Office lock file, because original MS Office file will be deleted.
            try
            {
                if (FsPath.IsMsOfficeLocked(userFileSystemOldPath))
                {
                    byte[] customData = new PlaceholderFile(userFileSystemOldPath).GetCustomData();
                    string userFileSystemLockFilePath = FsPath.GetLockPathFromMsOfficePath(userFileSystemOldPath);
                    new PlaceholderFile(userFileSystemLockFilePath).SetSavedData(customData);
                }
            }
            catch (Exception ex)
            {
                LogError("Failed so save custom data in MS Office lock file.", userFileSystemOldPath, ex);
            }

            // Process move.
            if (Engine.ChangesProcessingEnabled)
            {
                if (FsPath.Exists(userFileSystemOldPath))
                {
                    string remoteStorageOldPath = Mapping.MapPath(userFileSystemOldPath);
                    await new RemoteStorageItem(remoteStorageOldPath).MoveToAsync(userFileSystemNewPath, resultContext);
                }
            }
            else
            {
                resultContext.ReturnConfirmationResult();
            }

            // Read ETag from MS Office lock file, and store it back in original MS Office file.
            if (FsPath.IsMsOfficeLocked(userFileSystemNewPath))
            {
                string userFileSystemMsOfficeFileLockPath = FsPath.GetLockPathFromMsOfficePath(userFileSystemNewPath);
                byte[] customData = new PlaceholderFile(userFileSystemMsOfficeFileLockPath).GetSavedData();
                new PlaceholderFile(userFileSystemNewPath).SetCustomData(customData);
            }
        }

        ///<inheritdoc>
        public async Task DeleteAsync(IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            // Here we will simply delete the file in remote storage and confirm the operation.
            // In your implementation you may implement a more complex scenario with offline operations support.

            LogMessage("IFileSystemItem.DeleteAsync()", this.FullPath);

            string userFileSystemPath = this.FullPath;
            string remoteStoragePath = null;
            try
            {
                try
                {
                    Program.RemoteStorageMonitorInstance.Enabled = false; // Disable RemoteStorageMonitor to avoid circular calls.

                    remoteStoragePath = Mapping.MapPath(userFileSystemPath);
                    if (Engine.ChangesProcessingEnabled
                        && FsPath.Exists(remoteStoragePath) 
                        && !FsPath.AvoidSync(userFileSystemPath))
                    {

                        FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(remoteStoragePath);

                        if (remoteStorageItem is FileInfo)
                        {
                            remoteStorageItem.Delete();
                        }
                        else
                        {
                            (remoteStorageItem as DirectoryInfo).Delete(true);
                        }
                        LogMessage("Deleted succesefully:", remoteStoragePath);
                    }
                }
                finally
                {
                    resultContext.ReturnConfirmationResult();
                }
            }
            catch (Exception ex)
            {
                // remove try-catch when error processing inside CloudProvider is fixed.
                LogError("Delete failed:", remoteStoragePath, ex);
            }
            finally
            {
                Program.RemoteStorageMonitorInstance.Enabled = true;
            }
        }

        /// <summary>
        /// Simulates network delays and reports file transfer progress for demo purposes.
        /// </summary>
        /// <param name="fileLength">Length of file.</param>
        /// <param name="context">Context to report progress to.</param>
        protected void SimulateNetworkDelay(long fileLength, IResultContext resultContext)
        {
            if (Program.Settings.NetworkSimulationDelayMs > 0)
            {
                int numProgressResults = 5;
                for (int i = 0; i < numProgressResults; i++)
                {
                    resultContext.ReportProgress(fileLength, i * fileLength / numProgressResults);
                    Thread.Sleep(Program.Settings.NetworkSimulationDelayMs);
                }
            }
        }

        protected void LogError(string message, string sourcePath = null, Exception ex = null)
        {
            string att = FsPath.Exists(sourcePath) ? FsPath.GetAttString(sourcePath) : null;
            Logger.LogError($"{message,-45} {sourcePath,-80} {att} ", ex);
        }

        protected void LogMessage(string message, string sourcePath = null, string targetPath = null)
        {
            string att = FsPath.Exists(sourcePath) ? FsPath.GetAttString(sourcePath) : null;
            string size = FsPath.Size(sourcePath);
            Logger.LogMessage($"{message,-45} {sourcePath,-80} {size,7} {att} {targetPath}");
        }
    }
}
