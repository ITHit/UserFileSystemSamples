using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Samples.Common;
using ITHit.WebDAV.Client;

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
        /// <param name="logger">Logger.</param>
        public VirtualFolder(string path, VirtualEngine engine, ILogger logger) : base(path, engine, logger)
        {

        }

        /// <inheritdoc/>
        public async Task<string> CreateFileAsync(IFileMetadata fileMetadata, Stream content = null)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, fileMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFileAsync)}()", userFileSystemNewItemPath);

            Uri newFileUri = new Uri(new Uri(RemoteStoragePath), fileMetadata.Name);

            long contentLength = content != null ? content.Length : 0;

            IWebRequestAsync request = await Program.DavClient.GetFileWriteRequestAsync(newFileUri, null, contentLength);

            // Update remote storage file content.
            using (Stream davContentStream = await request.GetRequestStreamAsync())
            {
                if (content != null)
                {
                    await content.CopyToAsync(davContentStream);
                }

                // Get the new ETag returned by the server (if any).
                IWebResponseAsync response = await request.GetResponseAsync();
                string eTagNew = response.Headers["ETag"];
                response.Close();

                ExternalDataManager customDataManager = Engine.CustomDataManager(userFileSystemNewItemPath);

                // Store ETag unlil the next update.
                // This will also mark the item as not new, which is required for correct MS Office saving opertions.
                await customDataManager.ETagManager.SetETagAsync(eTagNew);
                customDataManager.IsNew = false; // Mark file as not new just in case the server did not return the ETag.

                // Update ETag in custom column displayed in file manager.
                await customDataManager.SetCustomColumnsAsync(new[] { new FileSystemItemPropertyData((int)CustomColumnIds.ETag, eTagNew) });
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<string> CreateFolderAsync(IFolderMetadata folderMetadata)
        {
            string userFileSystemNewItemPath = Path.Combine(UserFileSystemPath, folderMetadata.Name);
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(CreateFolderAsync)}()", userFileSystemNewItemPath);

            Uri newFolderUri = new Uri(new Uri(RemoteStoragePath), folderMetadata.Name);
            await Program.DavClient.CreateFolderAsync(newFolderUri);
            // Engine.CustomDataManager(userFileSystemNewItemPath).IsNew = false;

            return null;
        }

        /// <inheritdoc/>
        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call one of the following:
            // - resultContext.ReturnChildren() method.
            // - resultContext.ReportProgress() method.

            Logger.LogMessage($"{nameof(IFolder)}.{nameof(GetChildrenAsync)}({pattern})", UserFileSystemPath);

            IHierarchyItemAsync[] remoteStorageChildren = null;
            // Retry the request in case the log-in dialog is shown.
            try
            {
                remoteStorageChildren = await Program.DavClient.GetChildrenAsync(new Uri(RemoteStoragePath), false);
            }
            catch (ITHit.WebDAV.Client.Exceptions.Redirect302Exception)
            {
                remoteStorageChildren = await Program.DavClient.GetChildrenAsync(new Uri(RemoteStoragePath), false);
            }

            List<FileSystemItemMetadataExt> userFileSystemChildren = new List<FileSystemItemMetadataExt>();


            foreach (IHierarchyItemAsync remoteStorageItem in remoteStorageChildren)
            {
                FileSystemItemMetadataExt itemInfo = Mapping.GetUserFileSystemItemMetadata(remoteStorageItem);

                string userFileSystemItemPath = Path.Combine(UserFileSystemPath, itemInfo.Name);

                // Filtering existing files/folders. This is only required to avoid extra errors in the log.
                if (!FsPath.Exists(userFileSystemItemPath))
                {
                    Logger.LogMessage("Creating", userFileSystemItemPath);
                    userFileSystemChildren.Add(itemInfo);
                }

                ExternalDataManager customDataManager = Engine.CustomDataManager(userFileSystemItemPath);

                // Mark this item as not new, which is required for correct MS Office saving opertions.
                customDataManager.IsNew = false;
            }

            // To signal that the children enumeration is completed 
            // always call ReturnChildren(), even if the folder is empty.
            resultContext.ReturnChildren(userFileSystemChildren.ToArray(), userFileSystemChildren.Count());

            // Save ETags, the read-only attribute and all custom columns data.
            foreach (FileSystemItemMetadataExt child in userFileSystemChildren)
            {
                string userFileSystemItemPath = Path.Combine(UserFileSystemPath, child.Name);
                ExternalDataManager customDataManager = Engine.CustomDataManager(userFileSystemItemPath);

                // Save ETag on the client side, to be sent to the remote storage as part of the update.
                // Setting ETag also marks an item as not new.

                // ETags must correspond with a server file/folder, NOT with a client placeholder. 
                // It should NOT be moved/deleted/updated when a placeholder in the user file system is moved/deleted/updated.
                // It should be moved/deleted when a file/folder in the remote storage is moved/deleted.
                await customDataManager.ETagManager.SetETagAsync(child.ETag);

                // Set the read-only attribute and all custom columns data.
                await customDataManager.SetLockedByAnotherUserAsync(child.LockedByAnotherUser);
                await customDataManager.SetCustomColumnsAsync(child.CustomProperties);
            }
        }

        /// <inheritdoc/>
        public async Task WriteAsync(IFolderMetadata folderMetadata)
        {
            Logger.LogMessage($"{nameof(IFolder)}.{nameof(WriteAsync)}()", UserFileSystemPath);
        }
    }
}
