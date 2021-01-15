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
using VirtualFileSystem.Syncronyzation;
using Windows.Storage;

namespace VirtualFileSystem
{
    // In most cases you can use this class in your project without any changes.
    //$<IFolder
    /// <inheritdoc cref="IFolder"/>
    internal class VfsFolder : VfsFileSystemItem, IFolder
    {
        public VfsFolder(string path, ILogger logger, VfsEngine engine) : base(path, logger, engine)
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

            IEnumerable<FileSystemItemBasicInfo> children = await new UserFolder(UserFileSystemPath).EnumerateChildrenAsync(pattern);

            // Filtering existing files/folders. This is only required to avoid extra errors in the log.
            List<IFileSystemItemBasicInfo> newChildren = new List<IFileSystemItemBasicInfo>();
            foreach (IFileSystemItemBasicInfo child in children)
            {
                string userFileSystemItemPath = Path.Combine(UserFileSystemPath, child.Name);
                if (!FsPath.Exists(userFileSystemItemPath))
                {
                    Logger.LogMessage("Creating", child.Name);
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
                ETag.SetETagAsync(userFileSystemItemPath, child.ETag);

                // Set the lock icon and read-only attribute, to indicate that the item is locked by another user.
                new UserFileSystemRawItem(userFileSystemItemPath).SetLockedByAnotherUserAsync(child.LockedByAnotherUser);
            }
        }
    }
    //$>
}
