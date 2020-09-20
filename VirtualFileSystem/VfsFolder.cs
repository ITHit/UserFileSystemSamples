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
using Windows.Storage;

namespace VirtualFileSystem
{
    /// <inheritdoc/>
    public class VfsFolder : VfsFileSystemItem, IFolder
    {
        public VfsFolder(string path, ILogger logger) : base(path, logger)
        {

        }

        /// <inheritdoc/>
        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext)
        {
            // This method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call one of the following:
            // - IGetChildrenContext.ReturnChildren() method.
            // - IGetChildrenContext.ReportProgress() method.

            LogMessage($"IFolder.GetChildrenAsync({pattern})", this.FullPath);

            string remoreStoragePath = Mapping.MapPath(this.FullPath);

            IEnumerable<FileSystemInfo> remoteStorageChildren = new DirectoryInfo(remoreStoragePath).EnumerateFileSystemInfos(pattern);
            List<IFileSystemItemBasicInfo> userFileSystemChildren = new List<IFileSystemItemBasicInfo>();
            foreach (FileSystemInfo remoteStorageItem in remoteStorageChildren)
            {
                string userFileSystemPath = Path.Combine(this.FullPath, Path.GetFileName(remoteStorageItem.Name));
                if (!FsPath.Exists(userFileSystemPath))
                {
                    // Uncomment to simulate slow network access.
                    //Thread.Sleep(10000);
                    //resultContext.ReportProgress(remoteStorageChildren.Count(), userFileSystemChildren.Count());

                    LogMessage("Creating:", Path.GetFileName(remoteStorageItem.Name));
                    FileSystemItemBasicInfo userFileSystemItemInfo = Mapping.GetUserFileSysteItemInfo(remoteStorageItem);
                    userFileSystemChildren.Add(userFileSystemItemInfo);                    
                }
            }

            // To signal that the children enumeration is completed 
            // always call this method in GetChildrenAsync(), even if the folder is empty.
            resultContext.ReturnChildren(userFileSystemChildren.ToArray(), userFileSystemChildren.Count());
        }
    }
}
