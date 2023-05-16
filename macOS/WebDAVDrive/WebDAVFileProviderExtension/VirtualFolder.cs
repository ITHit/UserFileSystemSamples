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
        /// <param name="path">Folder path in the user file system.</param>
        /// <param name="session">WebDAV session.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFolder(string path, Client.WebDavSession session, ILogger logger) : base(path, session, logger)
        {

        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFileAsync(IFileMetadata fileMetadata, Stream content = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            Uri newFileUri = new Uri(await GetItemHrefAsync(), fileMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}()", newFileUri.AbsoluteUri);

            // Create a new file in the remote storage.
            long contentLength = content != null ? content.Length : 0;

            // Update remote storage file content.
            // Get the ETag returned by the server, if any.
            Client.IWebDavResponse<string> response = await Session.UploadAsync(newFileUri, async (outputStream) => {
                if (content != null)
                {
                    // Setting position to 0 is required in case of retry.
                    content.Position = 0;
                    await content.CopyToAsync(outputStream);
                }
            }, null, contentLength, 0, -1, null, null, null, cancellationToken);


            return Encoding.UTF8.GetBytes(response.Headers.GetValues("resource-id").FirstOrDefault() ?? string.Empty);
        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFolderAsync(IFolderMetadata folderMetadata, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            Uri newFolderUri = new Uri(await GetItemHrefAsync(), folderMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}()", newFolderUri.AbsoluteUri);

            await Session.CreateFolderAsync(newFolderUri, null, null, cancellationToken);

            // Store ETag (if any) unlil the next update here.
            // WebDAV server typically does not provide eTags for folders.
            // Engine.Placeholders.GetItem(userFileSystemNewItemPath).SetETag(eTag);

            // WebDAV does not use any item IDs, returning null.
            VirtualFolder newFolder = new VirtualFolder(newFolderUri.AbsoluteUri, Session, Logger);

            return (await newFolder.GetMetadataAsync()).RemoteStorageItemId;
        }

        /// <inheritdoc/>
        public async Task<IChanges> GetChangesAsync(string syncToken)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChangesAsync)}({syncToken})", RemoteStorageID);

            Client.IChanges davChanges = null;
            Changes changes = new Changes();
            changes.NewSyncToken = syncToken;

            Client.PropertyName[] propNames = new Client.PropertyName[2];
            propNames[0] = new Client.PropertyName("resource-id", "DAV:");
            propNames[1] = new Client.PropertyName("parent-resource-id", "DAV:");

            do
            {
                davChanges = (await Session.GetChangesAsync(new Uri(RemoteStorageID), propNames, changes.NewSyncToken, true)).WebDavResponse;
                changes.NewSyncToken = davChanges.NewSyncToken;

                foreach (Client.IChangedItem remoteStorageItem in davChanges)
                {
                    IChangedItem itemInfo = (IChangedItem)Mapping.GetUserFileSystemItemMetadata(remoteStorageItem);
                    itemInfo.ChangeType = remoteStorageItem.ChangeType == Client.Change.Changed ? Change.Changed : Change.Deleted;

                    changes.Add(itemInfo);
                }
            }
            while (davChanges.MoreResults);

            return changes;
        }

        /// <inheritdoc/>
        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext, CancellationToken cancellationToken)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChildrenAsync)}({pattern})", RemoteStorageID);

            Client.PropertyName[] propNames = new Client.PropertyName[2];
            propNames[0] = new Client.PropertyName("resource-id", "DAV:");
            propNames[1] = new Client.PropertyName("parent-resource-id", "DAV:");

            IList<Client.IHierarchyItem> remoteStorageChildren = (await Session.GetChildrenAsync(new Uri(RemoteStorageID), false, propNames, null, cancellationToken)).WebDavResponse;

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
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(WriteAsync)}()", RemoteStorageID, default, operationContext);
        }
    }
    
}
