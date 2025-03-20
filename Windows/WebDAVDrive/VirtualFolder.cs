using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
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
        /// <param name="autoLockTimeoutMs">Automatic lock timeout in milliseconds.</param>
        /// <param name="manualLockTimeoutMs">Manual lock timeout in milliseconds.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFolder(byte[] remoteStorageId, string userFileSystemPath, VirtualEngine engine, double autoLockTimeoutMs, double manualLockTimeoutMs, AppSettings appSettings, ILogger logger)
            : base(remoteStorageId, userFileSystemPath, engine, autoLockTimeoutMs, manualLockTimeoutMs, appSettings, logger)
        {

        }

        /// <inheritdoc/>
        public async Task<IFileMetadata> CreateFileAsync(IFileMetadata metadata, Stream content = null, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, metadata.Name);
            string contentParam = content != null ? content.Length.ToString() : "null";
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}({contentParam})", userFileSystemNewItemPath, default, operationContext, metadata);

            // Comment out the code below if you require a 0-lenght file to
            // be created in the remote storage as soon as possible. The Engine
            // will call IFile.WriteAsync() when the app completes writing to the file.
            if (content == null)
            {
                // If content is nul, we can not obtain a file handle.
                // The application is still writing into the file.
                inSyncResultContext.SetInSync = false;
                return null;
            }

            // Create a new file in the remote storage.
            Uri newFileUri = new Uri(new Uri(RemoteStoragePath), metadata.Name);

            // Send content to remote storage.
            // Get the ETag returned by the server, if any.
            long contentLength = content != null ? content.Length : 0;
            Client.IWebDavResponse<string> response = (await Dav.UploadAsync(newFileUri, async (outputStream) =>
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



            // Return newly created item to the Engine.
            // In the returned data set the following fields:
            //  - Remote storage item ID. It will be passed to GetFileSystemItem() during next calls.
            //  - Content eTag. The Engine will store it to determine if the file content should be updated.
            //  - Medatdata eTag. The Engine will store it to determine if the item metadata should be updated.

            byte[] remoteStorageItemId = null;
            if (response.Headers.Contains("resource-id"))
            {
                string remoteStorageId = response.Headers.GetValues("resource-id").FirstOrDefault();
                remoteStorageItemId = Encoding.UTF8.GetBytes(remoteStorageId);
            }
            return new FileMetadata()
            {
                RemoteStorageItemId = remoteStorageItemId,
                ContentETag = response.WebDavResponse
                // MetadataETag =
            };
        }

        /// <inheritdoc/>
        public async Task<IFolderMetadata> CreateFolderAsync(IFolderMetadata metadata, IOperationContext operationContext, IInSyncResultContext inSyncResultContext, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, metadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}()", userFileSystemNewItemPath, default, operationContext, metadata);

            Uri newFolderUri = new Uri(new Uri(RemoteStoragePath), metadata.Name);
            Client.IResponse response = await Dav.CreateFolderAsync(newFolderUri, null, null, cancellationToken);

            // Return newly created item to the Engine.
            // In the returned data set the following fields:
            //  - Remote storage item ID. It will be passed to GetFileSystemItem() during next calls.
            //  - Medatdata eTag. The Engine will store it to determine if the item metadata should be updated.

            byte[] remoteStorageItemId = null;
            if (response.Headers.Contains("resource-id"))
            {
                string remoteStorageId = response.Headers.GetValues("resource-id").FirstOrDefault();
                remoteStorageItemId = Encoding.UTF8.GetBytes(remoteStorageId);
            }
            return new FolderMetadata()
            {
                RemoteStorageItemId = remoteStorageItemId
                // MetadataETag =
            };
        }

        /// <inheritdoc/>
        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext, CancellationToken cancellationToken)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timeout timer call one of the following:
            // - resultContext.ReturnChildrenAsync() method.
            // - resultContext.ReportProgress() method.

            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChildrenAsync)}({pattern})", UserFileSystemPath, default, operationContext);

            // WebDAV Client lib will retry the request in case authentication is requested by the server.
            Client.IWebDavResponse<IList<Client.IHierarchyItem>> response = await Dav.GetChildrenAsync(new Uri(RemoteStoragePath), false, Mapping.GetDavProperties(), null, cancellationToken);

            List<IMetadata> children = new List<IMetadata>();

            foreach (Client.IHierarchyItem remoteStorageItem in response.WebDavResponse)
            {
                IMetadata itemInfo = Mapping.GetMetadata(remoteStorageItem);
                children.Add(itemInfo);
            }

            // To signal that the children enumeration is completed 
            // always call ReturnChildren(), even if the folder is empty.
            await resultContext.ReturnChildrenAsync(children.ToArray(), children.Count(), true, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IFolderMetadata> WriteAsync(IFolderMetadata metadata, IOperationContext operationContext, IInSyncResultContext inSyncResultContext, CancellationToken cancellationToken = default)
        {
            // Typically we can not change any folder metadata on a WebDAV server, just logging the call.
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(WriteAsync)}()", UserFileSystemPath, default, operationContext, metadata);

            // Return an updated item to the Engine.
            // In the returned data set the following fields:
            //  - Medatdata eTag. The Engine will store it to determine if the item metadata should be updated.
            return null;
        }

        /// <inheritdoc/>
        public async Task<IChanges> GetChangesAsync(string syncToken, bool deep, long? limit, CancellationToken cancellationToken)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChangesAsync)}({syncToken})", UserFileSystemPath);

            Client.IChanges davChanges = null;
            Changes changes = new Changes();
            changes.NewSyncToken = syncToken;

            do
            {
                davChanges = (await Dav.GetChangesAsync(
                    new Uri(RemoteStoragePath),
                    Mapping.GetDavProperties(),
                    changes.NewSyncToken,
                    deep,
                    limit,
                    cancellationToken: cancellationToken)).WebDavResponse;
                changes.NewSyncToken = davChanges.NewSyncToken;

                // Ordering results to make sure parents are created before children.
                IOrderedEnumerable<Client.IChangedItem> sortedChanges = davChanges.OrderBy(p => p.Href.AbsoluteUri.Length);

                foreach (Client.IChangedItem remoteStorageItem in sortedChanges)
                {
                    // Changed, created, moved and deleted item.
                    Change changeType = remoteStorageItem.ChangeType == Client.Change.Changed ? Change.Changed : Change.Deleted;

                    IMetadata itemInfo = Mapping.GetMetadata(remoteStorageItem, changeType);

                    ChangedItem changedItem = new ChangedItem(changeType, itemInfo);
                    changes.Add(changedItem);
                }
            }
            while (davChanges.MoreResults);

            // Returns changes to the Engine. Engine applies changes to the user file system and stores the new sync-token.
            return changes;
        }
    }
}
