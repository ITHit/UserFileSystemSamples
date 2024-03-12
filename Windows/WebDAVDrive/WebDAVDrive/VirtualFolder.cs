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
        public async Task<IFileMetadata> CreateFileAsync(IFileMetadata fileMetadata, Stream content = null, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, fileMetadata.Name);
            string contentParam = content != null ? content.Length.ToString() : "null";
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}({contentParam})", userFileSystemNewItemPath);

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
            Uri newFileUri = new Uri(new Uri(RemoteStoragePath), fileMetadata.Name);

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

            string remoteStorageId = response.Headers.GetValues("resource-id").FirstOrDefault();
            return new FileMetadataExt()
            {
                RemoteStorageItemId = Encoding.UTF8.GetBytes(remoteStorageId),
                ContentETag = response.WebDavResponse
                // MetadataETag =
            };
        }

        /// <inheritdoc/>
        public async Task<IFolderMetadata> CreateFolderAsync(IFolderMetadata folderMetadata, IOperationContext operationContext, IInSyncResultContext inSyncResultContext, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, folderMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}()", userFileSystemNewItemPath);

            Uri newFolderUri = new Uri(new Uri(RemoteStoragePath), folderMetadata.Name);
            Client.IResponse response = await Dav.CreateFolderAsync(newFolderUri, null, null, cancellationToken);

            // Return newly created item to the Engine.
            // In the returned data set the following fields:
            //  - Remote storage item ID. It will be passed to GetFileSystemItem() during next calls.
            //  - Medatdata eTag. The Engine will store it to determine if the item metadata should be updated.

            string remoteStorageId = response.Headers.GetValues("resource-id").FirstOrDefault();
            return new FolderMetadataExt()
            {
                RemoteStorageItemId = Encoding.UTF8.GetBytes(remoteStorageId)
                // MetadataETag =
            };
        }

        /// <inheritdoc/>
        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext, CancellationToken cancellationToken)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call one of the following:
            // - resultContext.ReturnChildren() method.
            // - resultContext.ReportProgress() method.

            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChildrenAsync)}({pattern})", UserFileSystemPath, default, operationContext);

            // WebDAV Client lib will retry the request in case authentication is requested by the server.
            var response = await Dav.GetChildrenAsync(new Uri(RemoteStoragePath), false, Mapping.GetDavProperties(), null, cancellationToken);

            List<FileSystemItemMetadataExt> children = new List<FileSystemItemMetadataExt>();

            foreach (Client.IHierarchyItem remoteStorageItem in response.WebDavResponse)
            {
                FileSystemItemMetadataExt itemInfo = Mapping.GetUserFileSystemItemMetadata(remoteStorageItem);
                children.Add(itemInfo);
            }

            // To signal that the children enumeration is completed 
            // always call ReturnChildren(), even if the folder is empty.
            await resultContext.ReturnChildrenAsync(children.ToArray(), children.Count());
        }

        /// <inheritdoc/>
        public async Task<IFolderMetadata> WriteAsync(IFileSystemBasicInfo fileBasicInfo, IOperationContext operationContext, IInSyncResultContext inSyncResultContext, CancellationToken cancellationToken = default)
        {
            // Typically we can not change any folder metadata on a WebDAV server, just logging the call.
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(WriteAsync)}()", UserFileSystemPath, default, operationContext);

            // Return an updated item to the Engine.
            // In the returned data set the following fields:
            //  - Medatdata eTag. The Engine will store it to determine if the item metadata should be updated.

            return null;
        }

        /// <inheritdoc/>
        public async Task<IChanges> GetChangesAsync(string syncToken, bool deep, long? limit, CancellationToken cancellationToken)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChangesAsync)}({syncToken})", UserFileSystemPath);

            // In this sample we use sync id algoritm for synchronization.
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
                    IFileSystemItemMetadata itemInfo = Mapping.GetUserFileSystemItemMetadata(remoteStorageItem);
                    // Changed, created, moved and deleted item.
                    Change changeType = remoteStorageItem.ChangeType == Client.Change.Changed ? Change.Changed : Change.Deleted;
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
