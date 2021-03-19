using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITHit.FileSystem.Samples.Common.Syncronyzation;
using Windows.Storage;
using Windows.Storage.Provider;

namespace ITHit.FileSystem.Samples.Common
{
    // In most cases you can use this class in your project without any changes.
    //$<IFolder
    /// <inheritdoc cref="IFolder"/>
    internal class VfsFolder : VfsFileSystemItem, IFolder
    {
        public VfsFolder(string path, ILogger logger, VfsEngine engine, VirtualDriveBase userEngine) : base(path, logger, engine, userEngine)
        {

        }

        /// <inheritdoc/>
        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call one of the following:
            // - resultContext.ReturnChildren() method.
            // - resultContext.ReportProgress() method.

            Logger.LogMessage($"IFolder.GetChildrenAsync({pattern})", UserFileSystemPath);

            IUserFolder userFolder = await VirtualDrive.GetItemAsync<IUserFolder>(UserFileSystemPath);
            IEnumerable<FileSystemItemBasicInfo> children = await userFolder.EnumerateChildrenAsync(pattern);

            // Filtering existing files/folders. This is only required to avoid extra errors in the log.
            List<IFileSystemItemBasicInfo> newChildren = new List<IFileSystemItemBasicInfo>();
            foreach (FileSystemItemBasicInfo child in children)
            {
                string userFileSystemItemPath = Path.Combine(UserFileSystemPath, child.Name);
                if (!FsPath.Exists(userFileSystemItemPath))
                {
                    Logger.LogMessage("Creating", child.Name);

                    // If the file is moved/renamed and the app is not running this will help us 
                    // to sync the file/folder to remote storage after app starts.
                    child.CustomData = new CustomData
                    {
                        OriginalPath = userFileSystemItemPath
                    }.Serialize();

                    newChildren.Add(child);
                }
            }

            // To signal that the children enumeration is completed 
            // always call ReturnChildren(), even if the folder is empty.
            resultContext.ReturnChildren(newChildren.ToArray(), newChildren.Count());

            
            // Save ETags and set "locked by another user" icon.
            foreach (FileSystemItemBasicInfo child in children)
            {
                string userFileSystemItemPath = Path.Combine(UserFileSystemPath, child.Name);

                // Create ETags.
                // ETags must correspond with a server file/folder, NOT with a client placeholder. 
                // It should NOT be moved/deleted/updated when a placeholder in the user file system is moved/deleted/updated.
                // It should be moved/deleted when a file/folder in the remote storage is moved/deleted.
                await ETag.SetETagAsync(userFileSystemItemPath, child.ETag);

                // Set the "locked by another user" icon and all custom columns data.
                await new UserFileSystemRawItem(userFileSystemItemPath).SetLockedByAnotherUserAsync(child.LockedByAnotherUser);
                await new UserFileSystemRawItem(userFileSystemItemPath).SetCustomColumnsDataAsync(child.CustomProperties);
            }
        }
    }
    //$>
}
