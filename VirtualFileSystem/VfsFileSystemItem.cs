using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Provider;

namespace VirtualFileSystem
{
    ///<inheritdoc>
    public abstract class VfsFileSystemItem : IFileSystemItem
    {
        /// <summary>
        /// File or folder path in user file system.
        /// </summary>
        protected readonly string FullPath;

        protected readonly ILogger Logger;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="path">File or folder path in user file system.</param>
        /// <param name="logger">Logger.</param>
        public VfsFileSystemItem(string path, ILogger logger)
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
        }


        ///<inheritdoc>
        public async Task MoveToAsync(string userFileSystemNewPath, IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            // Here we will simply move the file in remote storage and confirm the operation.
            // In your implementation you may implement a more complex scenario with offline operations support.

            LogMessage("IFileSystemItem.MoveToAsync()", this.FullPath, userFileSystemNewPath);

            string userFileSystemOldPath = this.FullPath;

            try
            {
                bool? inSync = null;
                try
                {
                    Program.RemoteStorageMonitorInstance.Enabled = false; // Disable RemoteStorageMonitor to avoid circular calls.

                    // When a file is deleted, it is moved to a Recycle Bin, that is why we check for recycle bin here.
                    if (FsPath.Exists(userFileSystemOldPath) && !FsPath.IsRecycleBin(userFileSystemNewPath) 
                        && !FsPath.AvoidSync(userFileSystemOldPath) && !FsPath.AvoidSync(userFileSystemNewPath))
                    {
                        inSync = PlaceholderItem.GetItem(userFileSystemOldPath).GetInSync();

                        string remoteStorageOldPath = Mapping.MapPath(userFileSystemOldPath);
                        string remoteStorageNewPath = Mapping.MapPath(userFileSystemNewPath);

                        FileSystemInfo remoteStorageOldItem = FsPath.GetFileSystemItem(remoteStorageOldPath);

                        if (remoteStorageOldItem is FileInfo)
                        {
                            (remoteStorageOldItem as FileInfo).MoveTo(remoteStorageNewPath);
                        }
                        else
                        {
                            (remoteStorageOldItem as DirectoryInfo).MoveTo(remoteStorageNewPath);
                        }
                        LogMessage("Moved succesefully:", remoteStorageOldPath, remoteStorageNewPath);
                    }
                }
                finally
                {
                    resultContext.ReturnConfirmationResult();

                    // If a file with content is deleted it is moved to a recycle bin and converted
                    // to a regular file, so placeholder features are not available on it.
                    if ( (inSync != null) && PlaceholderItem.IsPlaceholder(userFileSystemNewPath) )
                    {
                        PlaceholderItem.GetItem(userFileSystemNewPath).SetInSync(inSync.Value);
                    }
                }
            }
            catch(Exception ex)
            {
                // remove try-catch when error processing inside CloudProvider is fixed.
                LogError("Move failed:", $"From: {this.FullPath} to:{userFileSystemNewPath}", ex);
            }
            finally
            {
                Program.RemoteStorageMonitorInstance.Enabled = true;
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
                    if (FsPath.Exists(remoteStoragePath) && !FsPath.AvoidSync(userFileSystemPath))
                    {

                        FileSystemInfo remoteStorageItem = FsPath.GetFileSystemItem(remoteStoragePath);
                        remoteStorageItem.Delete();
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
