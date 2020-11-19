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
    // In most cases you can use this class in your project without any changes.
    ///<inheritdoc>
    internal abstract class VfsFileSystemItem : IFileSystemItem
    {
        /// <summary>
        /// File or folder path in the user file system.
        /// </summary>
        protected readonly string UserFileSystemPath;

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
        /// <param name="userFileSystemPath">File or folder path in user file system.</param>
        /// <param name="logger">Logger.</param>
        public VfsFileSystemItem(string userFileSystemPath, ILogger logger, VfsEngine engine)
        {
            if (string.IsNullOrEmpty(userFileSystemPath))
            {
                throw new ArgumentNullException("userFileSystemPath");
            }

            if(logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            UserFileSystemPath = userFileSystemPath;
            Logger = logger;
            Engine = engine;
        }

        //$<IFileSystemItem.MoveToAsync
        ///<inheritdoc>
        public async Task MoveToAsync(string userFileSystemNewPath, IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogMessage("IFileSystemItem.MoveToAsync()", userFileSystemOldPath, userFileSystemNewPath);

            // Process move.
            if (Engine.ChangesProcessingEnabled)
            {
                if (FsPath.Exists(userFileSystemOldPath))
                {
                    await new RemoteStorageRawItem(userFileSystemOldPath, Logger).MoveToAsync(userFileSystemNewPath, resultContext);
                }
            }
            else
            {
                resultContext.ReturnConfirmationResult();
            }

            // Restore Original Path and locked icon, lost during MS Office transactional save.
            if (FsPath.Exists(userFileSystemNewPath) &&  PlaceholderItem.IsPlaceholder(userFileSystemNewPath))
            {
                PlaceholderItem userFileSystemNewItem = PlaceholderItem.GetItem(userFileSystemNewPath);
                if (!userFileSystemNewItem.IsNew() && string.IsNullOrEmpty(userFileSystemNewItem.GetOriginalPath()))
                {
                    // Restore Original Path.
                    Logger.LogMessage("Saving Original Path", userFileSystemNewPath);
                    userFileSystemNewItem.SetOriginalPath(userFileSystemNewPath);

                    // Restore the 'locked' icon.
                    bool isLocked = await Lock.IsLockedAsync(userFileSystemNewPath);
                    await new UserFileSystemRawItem(userFileSystemNewPath).SetLockIconAsync(isLocked);
                }
            }
        }
        //$>

        //$<IFileSystemItem.DeleteAsync
        ///<inheritdoc>
        public async Task DeleteAsync(IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            Logger.LogMessage("IFileSystemItem.DeleteAsync()", this.UserFileSystemPath);

            string userFileSystemPath = this.UserFileSystemPath;
            string remoteStoragePath = null;
            try
            {
                if (Engine.ChangesProcessingEnabled
                    && !FsPath.AvoidSync(userFileSystemPath))
                {
                    await new RemoteStorageRawItem(userFileSystemPath, Logger).DeleteAsync();
                    Logger.LogMessage("Deleted item in remote storage succesefully", userFileSystemPath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Delete failed", remoteStoragePath, null, ex);
            }
            finally
            {
                resultContext.ReturnConfirmationResult();
            }
        }
        //$>

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
    }
}
