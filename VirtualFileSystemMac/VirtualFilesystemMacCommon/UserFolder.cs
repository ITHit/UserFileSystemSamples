using System;
using System.Threading.Tasks;
using System.IO;
using ITHit.FileSystem;

namespace VirtualFilesystemMacCommon
{
    public class UserFolder : IFolder
    {
        public readonly FileSystemItemBasicInfo FolderInfo;

        public UserFolder(string path)
        {
            FolderInfo = new FileSystemItemBasicInfo(path, File.GetAttributes(path), File.GetCreationTime(path), File.GetLastWriteTime(path), 0);
        }

        public Task GetChildrenAsync(string pattern, IOperationContext operationContext, IFolderListingResultContext resultContext)
        {
            return null;
        }


        public FileSystemItemBasicInfo[] GetChildren(string pattern)
        {
            string[] entries = Directory.GetFileSystemEntries(FolderInfo.Name, pattern);
            FileSystemItemBasicInfo[] infos = new FileSystemItemBasicInfo[entries.Length];

            for (int entryIndex = 0; entryIndex < entries.Length; ++entryIndex)
            {
                string currentFile = entries[entryIndex];
                ulong fileSize = 0;
                if (!File.GetAttributes(currentFile).HasFlag(FileAttributes.Directory))
                {
                    fileSize = (ulong)(new FileInfo(currentFile)).Length;
                }

                infos[entryIndex] = new FileSystemItemBasicInfo(currentFile, File.GetAttributes(currentFile), File.GetCreationTime(currentFile),
                                                                    File.GetLastWriteTime(currentFile), fileSize);
            }

            return infos;
        }

        public Task DeleteAsync(IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            Task<FileSystemItemBasicInfo> task = new Task<FileSystemItemBasicInfo>(() =>
            {
                return null;
            });

            return task;
        }

        public Task MoveToAsync(string targetPath, IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            Task<FileSystemItemBasicInfo> task = new Task<FileSystemItemBasicInfo>(() =>
            {
                return null;
            });

            return task;
        }
    }
}
