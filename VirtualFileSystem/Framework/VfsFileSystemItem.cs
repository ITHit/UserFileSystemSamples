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
                    await new RemoteStorageItem(userFileSystemOldPath).MoveToAsync(userFileSystemNewPath, resultContext);
                    string remoteStorageOldPath = Mapping.MapPath(userFileSystemOldPath);
                    string remoteStorageNewPath = Mapping.MapPath(userFileSystemNewPath);
                    Logger.LogMessage("Moved succesefully", remoteStorageOldPath, remoteStorageNewPath);
                }
            }
            else
            {
                resultContext.ReturnConfirmationResult();
            }

            // Restore Original Path, if it's lost during MS Office transactional save.
            if (FsPath.Exists(userFileSystemNewPath) &&  PlaceholderItem.IsPlaceholder(userFileSystemNewPath))
            {
                PlaceholderItem userFileSystemNewItem = PlaceholderItem.GetItem(userFileSystemNewPath);
                if (!userFileSystemNewItem.IsNew() && string.IsNullOrEmpty(userFileSystemNewItem.GetOriginalPath()))
                {
                    Logger.LogMessage("Saving Original Path", userFileSystemNewItem.Path);
                    userFileSystemNewItem.SetOriginalPath(userFileSystemNewItem.Path);
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
                remoteStoragePath = Mapping.MapPath(userFileSystemPath);

                if (Engine.ChangesProcessingEnabled
                    && !FsPath.AvoidSync(userFileSystemPath))
                {
                    await new RemoteStorageItem(userFileSystemPath).DeleteAsync();
                    Logger.LogMessage("Deleted succesefully", remoteStoragePath);
                }
            }
            catch (Exception ex)
            {
                // remove try-catch when error processing inside CloudProvider is fixed.
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
