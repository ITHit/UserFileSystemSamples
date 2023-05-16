using ITHit.FileSystem;
using ITHit.WebDAV.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Client = ITHit.WebDAV.Client;
using WebDAVCommon;

namespace WebDAVFileProviderExtension
{
    public abstract class VirtualFileSystemItem : IFileSystemItem
    {
        /// <summary>
        /// ID on the remote storage.
        /// </summary>
        protected readonly string RemoteStorageID;

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
        /// <param name="remoteStorageID">ID on the remote storage.</param>
        /// <param name="session">WebDAV session.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFileSystemItem(string remoteStorageID, WebDavSession session, ILogger logger)
        {
            if (string.IsNullOrEmpty(remoteStorageID))
            {
                throw new ArgumentNullException(nameof(remoteStorageID));
            }
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            RemoteStorageID = remoteStorageID;
            Session = session;
        }

        
        ///<inheritdoc>
        public async Task MoveToAsync(string targetUserFileSystemPath, byte[] targetFolderRemoteStorageItemId, IOperationContext operationContext = null, IConfirmationResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToAsync)}()", RemoteStorageID, targetUserFileSystemPath);
            Uri targetFolderUri = await new VirtualFolder(Encoding.UTF8.GetString(targetFolderRemoteStorageItemId), Session,Logger).GetItemHrefAsync();
            string targetItemName = targetUserFileSystemPath.Split(Path.DirectorySeparatorChar).Last();
            Uri targetItemUri = new Uri(targetFolderUri, targetItemName);

            await Session.MoveToAsync(await GetItemHrefAsync(), targetItemUri, true);
            Logger.LogMessage("Moved item in remote storage succesefully", RemoteStorageID, targetItemUri.AbsoluteUri);
        }
        

        /// <inheritdoc/>
        public async Task MoveToCompletionAsync(string targetUserFileSystemPath, byte[] targetFolderRemoteStorageItemId, IMoveCompletionContext moveCompletionContext = null, IInSyncStatusResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        
        ///<inheritdoc>
        public async Task DeleteAsync(IOperationContext operationContext = null, IConfirmationResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteAsync)}()", RemoteStorageID);

            await Session.DeleteAsync(new Uri(RemoteStorageID));
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
            Client.PropertyName[] propNames = new Client.PropertyName[2];
            propNames[0] = new Client.PropertyName("resource-id", "DAV:");
            propNames[1] = new Client.PropertyName("parent-resource-id", "DAV:");

            try
            {
                // Return IFileMetadata for a file, IFolderMetadata for a folder.
                item = (await Session.GetItemAsync(new Uri(RemoteStorageID), propNames)).WebDavResponse;
            }
            catch(ITHit.WebDAV.Client.Exceptions.NotFoundException e)
            {
                Logger.LogError($"{nameof(IFileSystemItem)}.{nameof(GetMetadataAsync)}()", RemoteStorageID, ex: e);

                item = null;
            }

            return item != null ? Mapping.GetUserFileSystemItemMetadata(item) : null;
        }


        /// <summary>
        /// Returns Uri of item.
        /// </summary>
        public async Task<Uri> GetItemHrefAsync()
        {
            return (await Session.GetItemAsync(new Uri(RemoteStorageID))).WebDavResponse.Href;
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
            string ext = System.IO.Path.GetExtension(RemoteStorageID).TrimStart('.');

            if (exts.Any(ext.Equals) || exts.Any("*".Equals))
            {
                string ThumbnailGeneratorUrl = AppGroupSettings.GetThumbnailGeneratorUrl()
                    .Replace("{thumbnail width}", "" + size)
                    .Replace("{thumbnail height}", "" + size);
                string filePathRemote = ThumbnailGeneratorUrl.Replace("{path to file}", RemoteStorageID);

                try
                {
                    using (IDownloadResponse response = await Session.DownloadAsync(new Uri(filePathRemote)))
                    {
                        using (Stream stream = await response.GetResponseStreamAsync())
                        {
                            thumbnail = await StreamToByteArrayAsync(stream);
                        }
                    }
                }
                catch (System.Net.WebException we)
                {
                    Logger.LogMessage(we.Message, RemoteStorageID);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Failed to load thumbnail {size}px", RemoteStorageID, null, e);
                }
            }

            string thumbnailResult = thumbnail != null ? "Success" : "Not Impl";
            Logger.LogMessage($"{nameof(VirtualEngine)}.{nameof(GetThumbnailAsync)}() - {thumbnailResult}", RemoteStorageID);

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
