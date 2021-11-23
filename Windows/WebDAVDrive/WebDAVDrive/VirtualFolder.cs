using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Samples.Common;
using Client = ITHit.WebDAV.Client;

namespace WebDAVDrive
{
    /// <inheritdoc cref="IFolder"/>
    public class VirtualFolder : VirtualFileSystemItem, IFolder, IVirtualFolder
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="path">Folder path in the user file system.</param>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFolder(string path, VirtualEngine engine, ILogger logger) : base(path, engine, logger)
        {

        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFileAsync(IFileMetadata fileMetadata, Stream content = null)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, fileMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}()", userFileSystemNewItemPath);

            Uri newFileUri = new Uri(new Uri(RemoteStoragePath), fileMetadata.Name);

            long contentLength = content != null ? content.Length : 0;

            // Update remote storage file content.
            // Get the new ETag returned by the server (if any).
            string eTagNew = await Program.DavClient.UploadAsync(newFileUri, async (outputStream) => {
                if (content != null)
                {
                    await content.CopyToAsync(outputStream);
                }
            }, null, contentLength);

            ExternalDataManager customDataManager = Engine.ExternalDataManager(userFileSystemNewItemPath);

            // Store ETag unlil the next update.
            await customDataManager.SetCustomDataAsync(
                eTagNew, 
                false,
                new[] { new FileSystemItemPropertyData((int)CustomColumnIds.ETag, eTagNew) });

            return null;
        }

        /// <inheritdoc/>
        public async Task<byte[]> CreateFolderAsync(IFolderMetadata folderMetadata)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, folderMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}()", userFileSystemNewItemPath);

            Uri newFolderUri = new Uri(new Uri(RemoteStoragePath), folderMetadata.Name);
            await Program.DavClient.CreateFolderAsync(newFolderUri);

            ExternalDataManager customDataManager = Engine.ExternalDataManager(userFileSystemNewItemPath);

            string eTagNew = ""; // WebDAV server sypically does not provide eTags for folders.

            // Store ETag unlil the next update.
            await customDataManager.SetCustomDataAsync(
                eTagNew,
                false,
                new[] { new FileSystemItemPropertyData((int)CustomColumnIds.ETag, eTagNew) });

            return null;
        }

        /// <inheritdoc/>
        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext)
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
                    Logger.LogMessage("Creating", userFileSystemItemPath);
                    userFileSystemChildren.Add(itemMetadata);
                }

                ExternalDataManager customDataManager = Engine.ExternalDataManager(userFileSystemItemPath);

                // Mark this item as not new, which is required for correct MS Office saving opertions.
                customDataManager.IsNew = false;
            }

            // To signal that the children enumeration is completed 
            // always call ReturnChildren(), even if the folder is empty.
            resultContext.ReturnChildren(userFileSystemChildren.ToArray(), userFileSystemChildren.Count());

            // Save ETags, the read-only attribute and all custom columns data.
            foreach (FileSystemItemMetadataExt itemMetadata in userFileSystemChildren)
            {
                string userFileSystemItemPath = Path.Combine(UserFileSystemPath, itemMetadata.Name);
                ExternalDataManager customDataManager = Engine.ExternalDataManager(userFileSystemItemPath);

                // Save ETag on the client side, to be sent to the remote storage as part of the update.
                await customDataManager.SetCustomDataAsync(itemMetadata.ETag, itemMetadata.IsLocked, itemMetadata.CustomProperties);
            }
        }

        public async Task<IEnumerable<FileSystemItemMetadataExt>> EnumerateChildrenAsync(string pattern)
        {
            // WebDAV Client lib will retry the request in case authentication is requested by the server.
            Client.IHierarchyItem[] remoteStorageChildren = await Program.DavClient.GetChildrenAsync(new Uri(RemoteStoragePath));

            List<FileSystemItemMetadataExt> userFileSystemChildren = new List<FileSystemItemMetadataExt>();

            foreach (Client.IHierarchyItem remoteStorageItem in remoteStorageChildren)
            {
                FileSystemItemMetadataExt itemInfo = Mapping.GetUserFileSystemItemMetadata(remoteStorageItem);
                userFileSystemChildren.Add(itemInfo);
            }

            return userFileSystemChildren;
        }

        /// <inheritdoc/>
        public async Task WriteAsync(IFolderMetadata folderMetadata, IOperationContext operationContext = null)
        {
            // We can not change any folder metadata on a WebDAV server, so this method is empty.
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(WriteAsync)}()", UserFileSystemPath, default, operationContext);
        }
    }
}
