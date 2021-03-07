using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using ITHit.FileSystem;

namespace FileProviderExtension
{
    public class VfsFolder : VfsFileSystemItem, IFolder, IFolderBasicInfo
    {
        public VfsFolder(string name, FileAttributes attributes,
                         DateTimeOffset creationTime, DateTimeOffset lastWriteTime, DateTimeOffset lastAccessTime)
            : base(name)
        {
            Name = name;
            Attributes = attributes;
            CreationTime = creationTime;
            LastWriteTime = lastWriteTime;
            LastAccessTime = lastAccessTime;
        }

        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext)
        {
            IEnumerable<FileSystemInfo> remoteStorageChildren = new DirectoryInfo(Mapping.MapPath(Name)).EnumerateFileSystemInfos(pattern);
            List<IFileSystemItemBasicInfo> infos = new List<IFileSystemItemBasicInfo>();

            foreach (FileSystemInfo remoteStorageItem in remoteStorageChildren)
            {
                VfsFileSystemItem info = (VfsFileSystemItem)Mapping.GetUserFileSysteItemBasicInfo(remoteStorageItem);
                info.Name = Mapping.ReverseMapPath(info.Name);

                infos.Add(info);
            }

            resultContext.ReturnChildren(infos.ToArray(), infos.Count);
        }

        public Task DeleteAsync(IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            throw new NotImplementedException();
        }

        public Task MoveToAsync(string targetPath, IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            throw new NotImplementedException();
        }
    }
}
