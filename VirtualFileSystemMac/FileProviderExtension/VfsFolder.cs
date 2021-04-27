using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using ITHit.FileSystem;
using VirtualFilesystemCommon;

namespace FileProviderExtension
{
    public class VfsFolder : VfsFileSystemItem, IFolder, IFolderMetadata
    {
        public VfsFolder(string name, FileAttributes attributes,
                         DateTimeOffset creationTime, DateTimeOffset lastWriteTime,
                         DateTimeOffset lastAccessTime, ILogger logger)
            : base(name, logger)
        {
            Name = name;
            Attributes = attributes;
            CreationTime = creationTime;
            LastWriteTime = lastWriteTime;
            LastAccessTime = lastAccessTime;
        }

        public async Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext)
        {
            Logger.LogMessage($"IFolder.GetChildrenAsync({pattern})", UserFileSystemPath);

            IEnumerable<FileSystemInfo> remoteStorageChildren = new DirectoryInfo(Mapping.MapPath(Name)).EnumerateFileSystemInfos(pattern);
            List<IFileSystemItemMetadata> infos = new List<IFileSystemItemMetadata>();

            foreach (FileSystemInfo remoteStorageItem in remoteStorageChildren)
            {
                VfsFileSystemItem info = (VfsFileSystemItem)Mapping.GetUserFileSysteItemBasicInfo(remoteStorageItem, Logger);
                info.Name = Mapping.ReverseMapPath(info.Name);

                infos.Add(info);
            }

            resultContext.ReturnChildren(infos.ToArray(), infos.Count);
        }           
    }
}
