using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITHit.FileSystem.Samples.Common.Windows.Syncronyzation;
using Windows.Storage;
using Windows.Storage.Provider;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    // In most cases you can use this class in your project without any changes.
    ///<inheritdoc>
    internal abstract class VfsFileSystemItem<TItemType> : IFileSystemItem where TItemType : IVirtualFileSystemItem
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
        /// Virtual drive.
        /// </summary>
        protected VirtualDriveBase VirtualDrive;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">File or folder path in the user file system.</param>
        /// <param name="logger">Logger.</param>
        public VfsFileSystemItem(string userFileSystemPath, ILogger logger, VfsEngine engine, VirtualDriveBase virtualDrive)
        {
            if (string.IsNullOrEmpty(userFileSystemPath))
            {
                throw new ArgumentNullException(nameof(userFileSystemPath));
            }

            if(logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            UserFileSystemPath = userFileSystemPath;
            Logger = logger;
            Engine = engine;
            VirtualDrive = virtualDrive;
        }

        //$<IFileSystemItem.MoveToAsync
        ///<inheritdoc>
        public async Task MoveToAsync(string userFileSystemNewPath, IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToAsync)}()", userFileSystemOldPath, userFileSystemNewPath);

            // Process move.
            if (Engine.ChangesProcessingEnabled)
            {
                if (FsPath.Exists(userFileSystemOldPath))
                {
                    await new RemoteStorageRawItem<TItemType>(userFileSystemOldPath, VirtualDrive, Logger).MoveToAsync(userFileSystemNewPath, resultContext);
                }
            }
            else
            {
                resultContext.ReturnConfirmationResult();
            }
        }
        //$>

        /// <inheritdoc/>
        public async Task MoveToCompletionAsync(IMoveCompletionContext moveCompletionContext, IResultContext resultContext)
        {
            string userFileSystemNewPath = this.UserFileSystemPath;
            string userFileSystemOldPath = moveCompletionContext.SourcePath;
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToCompletionAsync)}()", userFileSystemOldPath, userFileSystemNewPath);

            if (Engine.ChangesProcessingEnabled)
            {
                if (FsPath.Exists(userFileSystemNewPath))
                {
                    FileSystemItemTypeEnum itemType = FsPath.GetItemType(userFileSystemNewPath);
                    await new RemoteStorageRawItem<TItemType>(userFileSystemNewPath, VirtualDrive, Logger).MoveToCompletionAsync();
                }
            }
        }

        //$<IFileSystemItem.DeleteAsync
        ///<inheritdoc>
        public async Task DeleteAsync(IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            /*
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteAsync)}()", this.UserFileSystemPath);
            string userFileSystemPath = this.UserFileSystemPath;
            Logger.LogMessage("Confirming delete in user file system", userFileSystemPath);
            resultContext.ReturnConfirmationResult();
            */

            Logger.LogMessage("IFileSystemItem.DeleteAsync()", this.UserFileSystemPath);

            string userFileSystemPath = this.UserFileSystemPath;
            string remoteStoragePath = null;
            try
            {
                if (Engine.ChangesProcessingEnabled
                    && !FsPath.AvoidSync(userFileSystemPath))
                {
                    await new RemoteStorageRawItem<TItemType>(userFileSystemPath, VirtualDrive, Logger).DeleteAsync();
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

        /// <inheritdoc/>
        public async Task DeleteCompletionAsync(IOperationContext operationContext, IResultContext resultContext)
        {
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteCompletionAsync)}()", this.UserFileSystemPath);
            /*
            string userFileSystemPath = this.UserFileSystemPath;
            string remoteStoragePath = null;
            try
            {
                if (Engine.ChangesProcessingEnabled
                    && !FsPath.AvoidSync(userFileSystemPath))
                {
                    await new RemoteStorageRawItem<TItemType>(userFileSystemPath, VirtualDrive, Logger).DeleteAsync();
                    Logger.LogMessage("Deleted item in remote storage succesefully", userFileSystemPath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Delete in remote storage failed", remoteStoragePath, null, ex);

                // Rethrow the exception preserving stack trace of the original exception.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
            */
        }
        //$>

        ///<inheritdoc>
        public Task<IFileSystemItemMetadata> GetMetadataAsync()
        {
            // Return IFileMetadata for a file, IFolderMetadata for a folder.
            throw new NotImplementedException();
        }

        /// <summary>
        /// Simulates network delays and reports file transfer progress for demo purposes.
        /// </summary>
        /// <param name="fileLength">Length of file.</param>
        /// <param name="context">Context to report progress to.</param>
        protected void SimulateNetworkDelay(long fileLength, IResultContext resultContext)
        {
            if (VirtualDrive.Settings.NetworkSimulationDelayMs > 0)
            {
                int numProgressResults = 5;
                for (int i = 0; i < numProgressResults; i++)
                {
                    resultContext.ReportProgress(fileLength, i * fileLength / numProgressResults);
                    Thread.Sleep(VirtualDrive.Settings.NetworkSimulationDelayMs);
                }
            }
        }
    }
}
