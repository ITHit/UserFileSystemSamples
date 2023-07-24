using System.Text;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;
using ITHit.WebDAV.Client;
using WebDAVCommon;
using Client = ITHit.WebDAV.Client;

namespace WebDAVFileProviderExtension
{
    public abstract class VirtualFileSystemItem : IFileSystemItemMac
    {
        /// <summary>
        /// ID on the remote storage.
        /// </summary>
        protected readonly byte[] RemoteStorageId;

        /// <summary>
        /// Uri on the remote storage.
        /// </summary>
        protected readonly Uri RemoteStorageUriById;

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
        /// <param name="remoteStorageId">Id uri on the WebDav server.</param>
        /// <param name="session">WebDAV session.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFileSystemItem(byte[] remoteStorageId, WebDavSession session, ILogger logger)
        {
            if (remoteStorageId == null)
            {
                throw new ArgumentNullException(nameof(remoteStorageId));
            }

            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            RemoteStorageId = remoteStorageId;
            RemoteStorageUriById = Mapping.GetUriById(remoteStorageId);         

            Session = session;
        }

        
        ///<inheritdoc>
        public async Task MoveToAsync(string targetUserFileSystemPath, byte[] targetFolderRemoteStorageItemId, IOperationContext operationContext = null, IConfirmationResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(MoveToAsync)}()", RemoteStorageUriById.AbsoluteUri, targetUserFileSystemPath);
            Uri targetFolderUri = await new VirtualFolder(targetFolderRemoteStorageItemId, Session,Logger).GetItemHrefAsync();
            string targetItemName = targetUserFileSystemPath.Split(Path.DirectorySeparatorChar).Last();
            Uri targetItemUri = new Uri(targetFolderUri, targetItemName);

            await Session.MoveToAsync(await GetItemHrefAsync(), targetItemUri, true);
            Logger.LogMessage("Moved item in remote storage succesefully", RemoteStorageUriById.AbsoluteUri, targetItemUri.AbsoluteUri);
        }        
       
        ///<inheritdoc>
        public async Task DeleteAsync(IOperationContext operationContext = null, IConfirmationResultContext resultContext = null, CancellationToken cancellationToken = default)
        {
            Logger.LogMessage($"{nameof(IFileSystemItem)}.{nameof(DeleteAsync)}()", RemoteStorageUriById.AbsoluteUri);

            await Session.DeleteAsync(RemoteStorageUriById);
        }

        ///<inheritdoc>
        public async Task<IFileSystemItemMetadata?> GetMetadataAsync()
        {
            IHierarchyItem? item = null;
            Client.PropertyName[] propNames = new Client.PropertyName[2];
            propNames[0] = new Client.PropertyName("resource-id", "DAV:");
            propNames[1] = new Client.PropertyName("parent-resource-id", "DAV:");

            try
            {
                // Return IFileMetadata for a file, IFolderMetadata for a folder.
                item = (await Session.GetItemAsync(RemoteStorageUriById, propNames)).WebDavResponse;
            }
            catch(ITHit.WebDAV.Client.Exceptions.NotFoundException e)
            {
                Logger.LogError($"{nameof(IFileSystemItem)}.{nameof(GetMetadataAsync)}()", RemoteStorageUriById.AbsoluteUri, ex: e);

                item = null;
            }

            return item != null ? Mapping.GetUserFileSystemItemMetadata(item) : null;
        }

        /// <summary>
        /// Returns Uri of item.
        /// </summary>
        public async Task<Uri> GetItemHrefAsync()
        {
            return (await Session.GetItemAsync(RemoteStorageUriById)).WebDavResponse.Href;
        }


        ///<inheritdoc>
        public async Task<byte[]?> GetThumbnailAsync(uint size, IOperationContext operationContext)
        {
            byte[]? thumbnail = null;

            string[] exts = AppGroupSettings.GetRequestThumbnailsFor().Trim().Split("|");
            string ext = System.IO.Path.GetExtension(RemoteStorageUriById.AbsoluteUri).TrimStart('.');

            if (exts.Any(ext.Equals) || exts.Any("*".Equals))
            {
                string ThumbnailGeneratorUrl = AppGroupSettings.GetThumbnailGeneratorUrl()
                    .Replace("{thumbnail width}", "" + size)
                    .Replace("{thumbnail height}", "" + size);
                string filePathRemote = ThumbnailGeneratorUrl.Replace("{path to file}", RemoteStorageUriById.AbsoluteUri);

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
                    Logger.LogMessage(we.Message, RemoteStorageUriById.AbsoluteUri);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Failed to load thumbnail {size}px", RemoteStorageUriById.AbsoluteUri, null, e);
                }
            }

            string thumbnailResult = thumbnail != null ? "Success" : "Not Impl";
            Logger.LogMessage($"{nameof(VirtualEngine)}.{nameof(GetThumbnailAsync)}() - {thumbnailResult}", RemoteStorageUriById.AbsoluteUri);

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
        public Task<IEnumerable<FileSystemItemPropertyData>> GetPropertiesAsync(IOperationContext operationContext)
        {
            throw new NotImplementedException();
        }
    }
}
