using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

using Client = ITHit.WebDAV.Client;
using ITHit.FileSystem;
using ITHit.FileSystem.Synchronization;

using WebDAVFileProviderExtension.Synchronization;

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
            Uri newFileUri = new Uri(new Uri(RemoteStoragePath), fileMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}()", Path.Combine(UserFileSystemPath, fileMetadata.Name), targetPath: newFileUri.AbsoluteUri);

            // Create a new file in the remote storage.
            long contentLength = content != null ? content.Length : 0;

            // Update remote storage file content.
            // Get the ETag returned by the server, if any.
            string eTag = await Session.UploadAsync(newFileUri, async (outputStream) => {
                if (content != null)
                {
                    // Setting position to 0 is required in case of retry.
                    content.Position = 0;
                    await content.CopyToAsync(outputStream);
                }
            }, null, contentLength, 0, -1, null, null, null, cancellationToken);

            // WebDAV does not use any item IDs, returning null.
            return null;
        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFolderAsync(IFolderMetadata folderMetadata, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            Uri newFolderUri = new Uri(new Uri(RemoteStoragePath), folderMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}()",
                Path.Combine(UserFileSystemPath, folderMetadata.Name), targetPath: newFolderUri.AbsoluteUri);

            await Session.CreateFolderAsync(newFolderUri, null, null, cancellationToken);

            // Store ETag (if any) unlil the next update here.
            // WebDAV server typically does not provide eTags for folders.
            // Engine.Placeholders.GetItem(userFileSystemNewItemPath).SetETag(eTag);

            // WebDAV does not use any item IDs, returning null.
            return null;
        }

        /// <inheritdoc/>
        public async Task<IChanges> GetChangesAsync(string syncToken)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChangesAsync)}({syncToken})", UserFileSystemPath);

            Client.IChanges davChanges = null;
            Changes changes = new Changes();
            changes.NewSyncToken = syncToken;

            do
            {
                davChanges = await Session.GetChangesAsync(new Uri(RemoteStoragePath), null, changes.NewSyncToken, true);
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
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChildrenAsync)}({pattern})", UserFileSystemPath, RemoteStoragePath);

            Client.IHierarchyItem[] remoteStorageChildren = await Session.GetChildrenAsync(new Uri(RemoteStoragePath), false, null, null, cancellationToken);

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
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(WriteAsync)}()", UserFileSystemPath, default, operationContext);
        }
    }
    
}
