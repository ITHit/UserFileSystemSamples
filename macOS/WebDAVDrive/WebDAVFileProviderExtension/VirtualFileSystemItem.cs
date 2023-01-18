using ITHit.FileSystem;
using ITHit.WebDAV.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebDAVCommon;

namespace WebDAVFileProviderExtension
{
    public abstract class VirtualFileSystemItem : IFileSystemItem
    {
        /// <summary>
        /// File or folder path in the user file system.
        /// </summary>
        protected readonly string UserFileSystemPath;

        /// <summary>
        /// Path of this file or folder in the remote storage.
        /// </summary>
        protected readonly string RemoteStoragePath;

        /// <summary>
        /// Logger.
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// WebDav session.
        /// </summary>
        protected WebDavSession Session;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemPath">File or folder path in the user file system.</param>
        /// <param name="session">WebDAV session.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFileSystemItem(string userFileSystemPath, WebDavSession session, ILogger logger)
        {
            if (string.IsNullOrEmpty(userFileSystemPath))
            {
                throw new ArgumentNullException(nameof(userFileSystemPath));
            }
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            UserFileSystemPath = userFileSystemPath;
            Session = session;
            RemoteStoragePath = Mapping.MapPath(userFileSystemPath);
        }

        
        ///<inheritdoc>
        public async Task MoveToAsync(string targetUserFileSystemPath, byte[] targetFolderRemoteStorageItemId, IOperationContext operationContext = null, IConfirmationResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            string userFileSystemOldPath = this.UserFileSystemPath;
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToAsync)}()", UserFileSystemPath, targetUserFileSystemPath);
            string remoteStorageNewPath = Mapping.MapPath(targetUserFileSystemPath);
            string remoteStorageOldPath = RemoteStoragePath;

            await Session.MoveToAsync(new Uri(RemoteStoragePath), new Uri(remoteStorageNewPath), true);
            Logger.LogMessage("Moved item in remote storage succesefully", userFileSystemOldPath, targetUserFileSystemPath);
        }
        

        /// <inheritdoc/>
        public async Task MoveToCompletionAsync(string targetUserFileSystemPath, byte[] targetFolderRemoteStorageItemId, IMoveCompletionContext moveCompletionContext = null, IInSyncStatusResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        
        ///<inheritdoc>
        public async Task DeleteAsync(IOperationContext operationContext = null, IConfirmationResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteAsync)}()", UserFileSystemPath);

            await Session.DeleteAsync(new Uri(RemoteStoragePath));
        }

        /// <inheritdoc/>
        public async Task DeleteCompletionAsync(IOperationContext operationContext = null, IInSyncStatusResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
        

        ///<inheritdoc>
        public async Task<IFileSystemItemMetadata> GetMetadataAsync()
        {
            IHierarchyItem item = null;

            try
            {
                // Return IFileMetadata for a file, IFolderMetadata for a folder.
                item = await Session.GetItemAsync(RemoteStoragePath);
            }
            catch(ITHit.WebDAV.Client.Exceptions.NotFoundException)
            {
                item = null;
            }

            return item != null ? Mapping.GetUserFileSystemItemMetadata(item) : null;
        }

        /// <summary>
        /// Simulates network delays and reports file transfer progress for demo purposes.
        /// </summary>
        /// <param name="fileLength">Length of file.</param>
        /// <param name="context">Context to report progress to.</param>
        protected void SimulateNetworkDelay(long fileLength, IResultContext resultContext)
        {
            //Thread.Sleep(10000);
        }

        ///<inheritdoc>
        public async Task<byte[]> GetThumbnailAsync(uint size)
        {
            byte[] thumbnail = null;

            string[] exts = AppGroupSettings.GetRequestThumbnailsFor().Trim().Split("|");
            string ext = System.IO.Path.GetExtension(UserFileSystemPath).TrimStart('.');

            if (exts.Any(ext.Equals) || exts.Any("*".Equals))
            {
                string ThumbnailGeneratorUrl = AppGroupSettings.GetThumbnailGeneratorUrl()
                    .Replace("{thumbnail width}", "" + size)
                    .Replace("{thumbnail height}", "" + size);
                string filePathRemote = ThumbnailGeneratorUrl.Replace("{path to file}", Mapping.MapPath(UserFileSystemPath));

                try
                {
                    using (IWebResponse response = await Session.DownloadAsync(new Uri(filePathRemote)))
                    {
                        using (Stream stream = await response.GetResponseStreamAsync())
                        {
                            thumbnail = await StreamToByteArrayAsync(stream);
                        }
                    }
                }
                catch (System.Net.WebException we)
                {
                    Logger.LogMessage(we.Message, UserFileSystemPath);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Failed to load thumbnail {size}px", UserFileSystemPath, null, e);
                }
            }

            string thumbnailResult = thumbnail != null ? "Success" : "Not Impl";
            Logger.LogMessage($"{nameof(VirtualEngine)}.{nameof(GetThumbnailAsync)}() - {thumbnailResult}", UserFileSystemPath);

            return thumbnail;
        }

        private async Task<byte[]> StreamToByteArrayAsync(Stream stream)
        {
            using (MemoryStream memoryStream = new())
            {
                await stream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }

        ///<inheritdoc>
        public Task<IEnumerable<FileSystemItemPropertyData>> GetPropertiesAsync()
        {
            throw new NotImplementedException();
        }
    }
}
