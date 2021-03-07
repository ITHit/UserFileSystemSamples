using System;
using System.IO;
using System.Threading.Tasks;
using ITHit.FileSystem;

namespace VirtualFilesystemMacCommon
{
    public class UserFile : IFile
    {
        public readonly FileSystemItemBasicInfo FileInfo;

        public UserFile(string path)
        {
            ulong fileSize = (ulong)(new FileInfo(path)).Length;
            FileInfo = new FileSystemItemBasicInfo(path, System.IO.File.GetAttributes(path), System.IO.File.GetCreationTime(path),
                                                       System.IO.File.GetLastWriteTime(path), fileSize);
        }

        public Task CloseAsync(IOperationContext operationContext, IResultContext context)
        {
            Task<FileSystemItemBasicInfo> task = new Task<FileSystemItemBasicInfo>(() =>
            {
                return null;
            });

            return task;
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

        public Task OpenAsync(IOperationContext operationContext, IResultContext context)
        {
            Task<FileSystemItemBasicInfo> task = new Task<FileSystemItemBasicInfo>(() =>
            {
                return null;
            });

            return task;
        }

        public Task TransferDataAsync(long offset, long length, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext)
        {
            Task<byte[]> task = new Task<byte[]>(() =>
            {
                return null;
            });

            return task;
        }

        public Task ValidateDataAsync(long offset, long length, IValidateDataOperationContext operationContext, IValidateDataResultContext resultContext)
        {
            Task<byte[]> task = new Task<byte[]>(() =>
            {
                return null;
            });

            return task;
        }
    }
}
