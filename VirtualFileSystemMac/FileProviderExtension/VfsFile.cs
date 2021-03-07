using System;
using System.IO;
using System.Threading.Tasks;
using ITHit.FileSystem;

namespace FileProviderExtension
{
    public class VfsFile : VfsFileSystemItem, IFile, IFileBasicInfo
    {
        public long Length { get; set; }

        public VfsFile(string name, FileAttributes attributes,
                        DateTimeOffset creationTime, DateTimeOffset lastWriteTime, DateTimeOffset lastAccessTime, long length)
            : base(name)
        {
            Name = name;
            Attributes = attributes;
            CreationTime = creationTime;
            LastWriteTime = lastWriteTime;
            LastAccessTime = lastAccessTime;
            Length = length;
        }

        public async Task CloseAsync(IOperationContext operationContext, IResultContext context)
        {
            throw new NotImplementedException();
        }

        public async Task DeleteAsync(IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            throw new NotImplementedException();
        }

        public async Task MoveToAsync(string targetPath, IOperationContext operationContext, IConfirmationResultContext resultContext)
        {
            throw new NotImplementedException();
        }

        public async Task OpenAsync(IOperationContext operationContext, IResultContext context)
        {
            throw new NotImplementedException();
        }

        public async Task TransferDataAsync(long offset, long length, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext)
        {
            throw new NotImplementedException();
        }

        public async Task ValidateDataAsync(long offset, long length, IValidateDataOperationContext operationContext, IValidateDataResultContext resultContext)
        {
            throw new NotImplementedException();
        }
    }
}
