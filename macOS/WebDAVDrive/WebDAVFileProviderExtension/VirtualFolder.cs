using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

using Client = ITHit.WebDAV.Client;
using ITHit.FileSystem;
using ITHit.FileSystem.Synchronization;

using System.Text;

namespace WebDAVFileProviderExtension
{

    /// <inheritdoc cref="IFolder"/>
    public class VirtualFolder : VirtualFileSystemItem, IFolder, ISynchronizationCollection
    {

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="remoteStorageId">Id uri on the WebDav server.</param>
        /// <param name="engine">Engine.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFolder(byte[] remoteStorageId, VirtualEngine engine, ILogger logger) : base(remoteStorageId, engine, logger)
        {

        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFileAsync(IFileMetadata fileMetadata, Stream? content = null, IInSyncResultContext? inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            Uri newFileUri = new Uri(await GetItemHrefAsync(), fileMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}()", newFileUri.AbsoluteUri);

            // Create a new file in the remote storage.
            long contentLength = content != null ? content.Length : 0;

            Client.IWebDavResponse<string> response = await Engine.WebDavSession.UploadAsync(newFileUri, async (outputStream) => {
                if (content != null)
                {
                    // Setting position to 0 is required in case of retry.
                    content.Position = 0;
                    await content.CopyToAsync(outputStream);
                }
            }, null, contentLength, 0, -1, null, null, null, cancellationToken);

            // Return new item remove storage id to the Engine.
            // It will be past to GetFileSystemItemAsync during next call.
            return Encoding.UTF8.GetBytes(response.Headers.GetValues("resource-id").FirstOrDefault() ?? string.Empty);
        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFolderAsync(IFolderMetadata folderMetadata, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            Uri newFolderUri = new Uri(await GetItemHrefAsync(), folderMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}()", newFolderUri.AbsoluteUri);

            Client.IResponse response = await Engine.WebDavSession.CreateFolderAsync(newFolderUri, null, null, cancellationToken);

            // Return new item remove storage id to the Engine.
            // It will be past to GetFileSystemItemAsync during next call.
            return Encoding.UTF8.GetBytes(response.Headers.GetValues("resource-id").FirstOrDefault());
        }

        /// <inheritdoc/>
        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext, CancellationToken cancellationToken)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChildrenAsync)}({pattern})", RemoteStorageUriById.AbsoluteUri);

            Client.PropertyName[] propNames = new Client.PropertyName[2];
            propNames[0] = new Client.PropertyName("resource-id", "DAV:");
            propNames[1] = new Client.PropertyName("parent-resource-id", "DAV:");

            IList<Client.IHierarchyItem> remoteStorageChildren = (await Engine.WebDavSession.GetChildrenAsync(RemoteStorageUriById, false, propNames, null, cancellationToken)).WebDavResponse;

            List<IFileSystemItemMetadata> userFileSystemChildren = new List<IFileSystemItemMetadata>();
            foreach (Client.IHierarchyItem remoteStorageItem in remoteStorageChildren)
            {
                IFileSystemItemMetadata itemInfo = Mapping.GetUserFileSystemItemMetadata(remoteStorageItem);
                userFileSystemChildren.Add(itemInfo);                             
            }

            // To signal that the children enumeration is completed 
            // always call ReturnChildren(), even if the folder is empty.
            await resultContext.ReturnChildrenAsync(userFileSystemChildren.ToArray(), userFileSystemChildren.Count);
        }

        /// <inheritdoc/>
        public async Task WriteAsync(IFolderMetadata folderMetadata, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            // Typically we can not change any folder metadata on a WebDAV server, just logging the call.
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(WriteAsync)}()", RemoteStorageUriById.AbsoluteUri, default, operationContext);
        }

        /// <inheritdoc/>
        public async Task<IChanges> GetChangesAsync(string syncToken, bool deep, long? limit, CancellationToken cancellationToken)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChangesAsync)}({syncToken})", RemoteStorageUriById.AbsoluteUri);

            Client.IChanges davChanges;
            Changes changes = new Changes();
            changes.NewSyncToken = syncToken;

            // In this sample we use sync id algoritm for synchronization.
            Client.PropertyName[] propNames = new Client.PropertyName[2];
            propNames[0] = new Client.PropertyName("resource-id", "DAV:");
            propNames[1] = new Client.PropertyName("parent-resource-id", "DAV:");

            do
            {
                davChanges = (await Engine.WebDavSession.GetChangesAsync(RemoteStorageUriById, propNames, changes.NewSyncToken, deep, limit, cancellationToken: cancellationToken)).WebDavResponse;
                changes.NewSyncToken = davChanges.NewSyncToken;

                foreach (Client.IChangedItem remoteStorageItem in davChanges)
                {
                    IChangedItem itemInfo = (IChangedItem)Mapping.GetUserFileSystemItemMetadata(remoteStorageItem);
                    // Changed, created, moved and deleted item.
                    itemInfo.ChangeType = remoteStorageItem.ChangeType == Client.Change.Changed ? Change.Changed : Change.Deleted;

                    changes.Add(itemInfo);
                }
            }
            while (davChanges.MoreResults);

            // Returns changes to the Engine. Engine applies changes to the user file system and stores the new sync-token.
            return changes;
        }
    }
    
}
