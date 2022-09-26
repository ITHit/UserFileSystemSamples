using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Samples.Common;
using Client = ITHit.WebDAV.Client;


namespace WebDAVDrive
{
    /// <inheritdoc cref="IFolder"/>
    public class VirtualFolder : VirtualFileSystemItem, IFolder
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="path">Folder path in the user file system.</param>
        /// <param name="engine">Engine instance.</param>
        /// <param name="autoLockTimoutMs">Automatic lock timout in milliseconds.</param>
        /// <param name="manualLockTimoutMs">Manual lock timout in milliseconds.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFolder(string path, VirtualEngine engine, double autoLockTimoutMs, double manualLockTimoutMs, ILogger logger) 
            : base(path, engine, autoLockTimoutMs, manualLockTimoutMs, logger)
        {

        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFileAsync(IFileMetadata fileMetadata, Stream content = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, fileMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}()", userFileSystemNewItemPath);

            // Create a new file in the remote storage.
            Uri newFileUri = new Uri(new Uri(RemoteStoragePath), fileMetadata.Name);

            long contentLength = content != null ? content.Length : 0;

            // Update remote storage file content.
            // Get the ETag returned by the server, if any.
            string eTag = await Program.DavClient.UploadAsync(newFileUri, async (outputStream) => {
                if (content != null)
                {
                    // Setting position to 0 is required in case of retry.
                    content.Position = 0;
                    await content.CopyToAsync(outputStream);
                }
            }, null, contentLength, 0, -1, null, null, cancellationToken);

            // Store ETag in persistent placeholder properties untill the next update.
            Engine.Placeholders.GetItem(userFileSystemNewItemPath).SetETag(eTag);
            
            // WebDAV does not use any item IDs, returning null.
            return null;
        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFolderAsync(IFolderMetadata folderMetadata, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, folderMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}()", userFileSystemNewItemPath);

            Uri newFolderUri = new Uri(new Uri(RemoteStoragePath), folderMetadata.Name);
            await Program.DavClient.CreateFolderAsync(newFolderUri, null, cancellationToken);

            // Store ETag (if any) unlil the next update here.
            // WebDAV server typically does not provide eTags for folders.
            // Engine.Placeholders.GetItem(userFileSystemNewItemPath).SetETag(eTag);

            // WebDAV does not use any item IDs, returning null.
            return null;
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
                string userFileSystemItemPath = Path.Combine(UserFileSystemPath, itemMetadata.Name);

                // Filtering existing files/folders. This is only required to avoid extra errors in the log.
                if (!FsPath.Exists(userFileSystemItemPath))
                {
                    Logger.LogDebug("Creating", userFileSystemItemPath, null, operationContext);
                    userFileSystemChildren.Add(itemMetadata);
                }
            }

            // To signal that the children enumeration is completed 
            // always call ReturnChildren(), even if the folder is empty.
            await resultContext.ReturnChildrenAsync(userFileSystemChildren.ToArray(), userFileSystemChildren.Count());

            // Save data that will be displayes in custom columns in file manager
            // as well as any additional custom data required by the client.
            foreach (FileSystemItemMetadataExt itemMetadata in userFileSystemChildren)
            {
                string userFileSystemItemPath = Path.Combine(UserFileSystemPath, itemMetadata.Name);
                await Engine.Placeholders.GetItem(userFileSystemItemPath).SavePropertiesAsync(itemMetadata);
            }
        }

        public async Task<IEnumerable<FileSystemItemMetadataExt>> EnumerateChildrenAsync(string pattern, CancellationToken cancellationToken = default)
        {
            // WebDAV Client lib will retry the request in case authentication is requested by the server.
            Client.IHierarchyItem[] remoteStorageChildren = await Program.DavClient.GetChildrenAsync(new Uri(RemoteStoragePath), false, null, cancellationToken);

            List<FileSystemItemMetadataExt> userFileSystemChildren = new List<FileSystemItemMetadataExt>();

            foreach (Client.IHierarchyItem remoteStorageItem in remoteStorageChildren)
            {
                FileSystemItemMetadataExt itemInfo = Mapping.GetUserFileSystemItemMetadata(remoteStorageItem);
                userFileSystemChildren.Add(itemInfo);
            }

            return userFileSystemChildren;
        }

        /// <inheritdoc/>
        public async Task WriteAsync(IFolderMetadata folderMetadata, IOperationContext operationContext = null, IInSyncResultContext inSyncResultContext = null, CancellationToken cancellationToken = default)
        {
            // Typically we can not change any folder metadata on a WebDAV server, just logging the call.
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(WriteAsync)}()", UserFileSystemPath, default, operationContext);
        }
    }
}
