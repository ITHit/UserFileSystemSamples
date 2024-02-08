using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Samples.Common;
using Client = ITHit.WebDAV.Client;
using ITHit.FileSystem.Synchronization;


namespace WebDAVDrive
{
    /// <inheritdoc cref="IFolder"/>
    public class VirtualFolder : VirtualFileSystemItem, IFolder, ISynchronizationCollection
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="remoteStorageId">Remote storage item ID.</param>
        /// <param name="userFileSystemPath">User file system path. This paramater is available on Windows platform only. On macOS and iOS this parameter is always null</param>
        /// <param name="engine">Engine instance.</param>
        /// <param name="autoLockTimoutMs">Automatic lock timout in milliseconds.</param>
        /// <param name="manualLockTimoutMs">Manual lock timout in milliseconds.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFolder(byte[] remoteStorageId, string userFileSystemPath, VirtualEngine engine, double autoLockTimoutMs, double manualLockTimoutMs, ILogger logger)
            : base(remoteStorageId, userFileSystemPath, engine, autoLockTimoutMs, manualLockTimoutMs, logger)
        {

        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFileAsync(IFileMetadata fileMetadata, Stream content = null, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, fileMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}()", userFileSystemNewItemPath);

            // Create a new file in the remote storage.
            Uri newFileUri = new Uri(new Uri(RemoteStoragePath), fileMetadata.Name);

            long contentLength = content != null ? content.Length : 0;

            // Send content to remote storage.
            // Get the ETag returned by the server, if any.
            Client.IWebDavResponse<string> response = (await Program.DavClient.UploadAsync(newFileUri, async (outputStream) =>
            {
                if (content != null)
                {
                    // Setting position to 0 is required in case of retry.
                    content.Position = 0;
                    await content.CopyToAsync(outputStream);
                }
            }, null, contentLength, 0, -1, null, null, null, cancellationToken));

            //switch (response.Status.Code)
            //{
            //    case 201: // Client.HttpStatus.Created:
            //        break;
            //    case 200: // Client.HttpStatus.OK: The file already exists and eTags matched.
            //        break;
            //}


            // Store ETag in persistent placeholder properties untill the next update.
            operationContext.Properties.SetETag(response.WebDavResponse);

            // Return newly created item remote storage item ID,
            // it will be passed to GetFileSystemItem() during next calls.
            string remoteStorageId = response.Headers.GetValues("resource-id").FirstOrDefault();
            return Encoding.UTF8.GetBytes(remoteStorageId);
        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFolderAsync(IFolderMetadata folderMetadata, IOperationContext operationContext, IInSyncResultContext inSyncResultContext, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, folderMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}()", userFileSystemNewItemPath);

            Uri newFolderUri = new Uri(new Uri(RemoteStoragePath), folderMetadata.Name);
            Client.IResponse response = await Program.DavClient.CreateFolderAsync(newFolderUri, null, null, cancellationToken);

            // Store ETag (if any) unlil the next update here.
            // WebDAV server typically does not provide eTags for folders.
            // Engine.Placeholders.GetItem(userFileSystemNewItemPath).SetETag(eTag);

            // Return newly created item remote storage item ID,
            // it will be passed to GetFileSystemItem() during next calls.
            string remoteStorageId = response.Headers.GetValues("resource-id").FirstOrDefault();
            return Encoding.UTF8.GetBytes(remoteStorageId);
        }

        /// <inheritdoc/>
        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext, CancellationToken cancellationToken)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call one of the following:
            // - resultContext.ReturnChildren() method.
            // - resultContext.ReportProgress() method.

            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChildrenAsync)}({pattern})", UserFileSystemPath, default, operationContext);

            IEnumerable<FileSystemItemMetadataExt> remoteStorageChildren = await EnumerateChildrenAsync(pattern);

            List<FileSystemItemMetadataExt> userFileSystemChildren = new List<FileSystemItemMetadataExt>();
            foreach (FileSystemItemMetadataExt itemMetadata in remoteStorageChildren)
            {
                userFileSystemChildren.Add(itemMetadata);
            }

            // To signal that the children enumeration is completed 
            // always call ReturnChildren(), even if the folder is empty.
            await resultContext.ReturnChildrenAsync(userFileSystemChildren.ToArray(), userFileSystemChildren.Count());
        }

        public async Task<IEnumerable<FileSystemItemMetadataExt>> EnumerateChildrenAsync(string pattern, CancellationToken cancellationToken = default)
        {
            Client.PropertyName[] propNames = new Client.PropertyName[2];
            propNames[0] = new Client.PropertyName("resource-id", "DAV:");
            propNames[1] = new Client.PropertyName("parent-resource-id", "DAV:");

            // WebDAV Client lib will retry the request in case authentication is requested by the server.
            IList<Client.IHierarchyItem> remoteStorageChildren = (await Program.DavClient.GetChildrenAsync(new Uri(RemoteStoragePath), false, propNames, null, cancellationToken)).WebDavResponse;

            List<FileSystemItemMetadataExt> userFileSystemChildren = new List<FileSystemItemMetadataExt>();

            foreach (Client.IHierarchyItem remoteStorageItem in remoteStorageChildren)
            {
                FileSystemItemMetadataExt itemInfo = Mapping.GetUserFileSystemItemMetadata(remoteStorageItem);
                userFileSystemChildren.Add(itemInfo);
            }

            return userFileSystemChildren;
        }

        /// <inheritdoc/>
        public async Task<IFolderMetadata> WriteAsync(IFileSystemBasicInfo fileBasicInfo, IOperationContext operationContext, IInSyncResultContext inSyncResultContext, CancellationToken cancellationToken = default)
        {
            // Typically we can not change any folder metadata on a WebDAV server, just logging the call.
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(WriteAsync)}()", UserFileSystemPath, default, operationContext);
            return null;
        }

        /// <inheritdoc/>
        public async Task<IChanges> GetChangesAsync(string syncToken, bool deep, long? limit, CancellationToken cancellationToken)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChangesAsync)}({syncToken})", UserFileSystemPath);

            Client.IChanges davChanges = null;
            Changes changes = new Changes();
            changes.NewSyncToken = syncToken;

            // In this sample we use sync id algoritm for synchronization.
            Client.PropertyName[] propNames = new Client.PropertyName[3];
            propNames[0] = new Client.PropertyName("resource-id", "DAV:");
            propNames[1] = new Client.PropertyName("parent-resource-id", "DAV:");
            propNames[2] = new Client.PropertyName("Etag", "DAV:");

            do
            {
                davChanges = (await Program.DavClient.GetChangesAsync(
                    new Uri(RemoteStoragePath),
                    propNames,
                    changes.NewSyncToken,
                    deep,
                    limit,
                    cancellationToken: cancellationToken)).WebDavResponse;
                changes.NewSyncToken = davChanges.NewSyncToken;

                // Ordering results to make sure parents are created before children.
                IOrderedEnumerable<Client.IChangedItem> sortedChanges = davChanges.OrderBy(p => p.Href.AbsoluteUri.Length);

                foreach (Client.IChangedItem remoteStorageItem in sortedChanges)
                {
                    IFileSystemItemMetadata itemInfo = Mapping.GetUserFileSystemItemMetadata(remoteStorageItem);
                    // Changed, created, moved and deleted item.
                    Change changeType = remoteStorageItem.ChangeType == Client.Change.Changed ? Change.Changed : Change.Deleted;

                    ChangedItem changedItem = new ChangedItem(changeType, itemInfo);

                    if (changeType == Change.Changed)
                    {
                        // If remote storage Etag does not match client Etag the file content
                        // is modified in the remote storage and must be downloaded (for hydrated items).
                        if (Engine.Placeholders.TryFindByRemoteStorageId(itemInfo.RemoteStorageItemId, out PlaceholderItem placeholderItem))
                        {
                            if(await placeholderItem.IsModifiedAsync(itemInfo as FileSystemItemMetadataExt))
                            {
                                changedItem.ChangeType = Change.MetadataAndContent;
                            }
                        }
                    }
                    changes.Add(changedItem);
                }
            }
            while (davChanges.MoreResults);

            // Returns changes to the Engine. Engine applies changes to the user file system and stores the new sync-token.
            return changes;
        }
    }
}
