using System;
using System.IO;
using System.Threading.Tasks;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;
using VirtualFilesystemCommon;

namespace FileProviderExtension
{
    public class VfsFile : VfsFileSystemItem, IFileMac, IFileMetadata
    {
        public long Length { get; set; }

        public VfsFile(string name, FileAttributes attributes,
                        DateTimeOffset creationTime, DateTimeOffset lastWriteTime, DateTimeOffset lastAccessTime, long length, ILogger logger)
            : base(name, logger)
        {
            Name = name;
            Attributes = attributes;
            CreationTime = creationTime;
            LastWriteTime = lastWriteTime;
            LastAccessTime = lastAccessTime;
            Length = length;
        }        


        public async Task OpenAsync(IOperationContext operationContext, IResultContext context)
        {
            throw new NotImplementedException();
        }

        public async Task TransferDataAsync(long offset, long length, ITransferDataOperationContext operationContext, ITransferDataResultContext resultContext)
        {
            Logger.LogMessage($"IFile.TransferDataAsync({offset}, {length})", UserFileSystemPath);

            byte[] buffer = new byte[length];

            await using (FileStream stream = File.OpenRead(RemoteStoragePath))
            {
                stream.Seek(offset, SeekOrigin.Begin);
              
                int bytesRead = await stream.ReadAsync(buffer, 0, (int)length);
            }
           
            resultContext.ReturnData(buffer, offset, length);
        }

        public async Task ValidateDataAsync(long offset, long length, IValidateDataOperationContext operationContext, IValidateDataResultContext resultContext)
        {
            throw new NotImplementedException();
        }

        public async Task UpdateAsync(Stream stream, IResultContext context)
        {
            Logger.LogMessage($"IFile.UpdateAsync", UserFileSystemPath);

            Logger.LogMessage("Sending to remote storage", UserFileSystemPath);
            FileInfo remoteStorageItem = new FileInfo(RemoteStoragePath);
            await using (FileStream remoteStorageStream = remoteStorageItem.Open(FileMode.Open, FileAccess.Write, FileShare.None))
            {
                string userFileSystemPath = Mapping.ReverseMapPath(RemoteStoragePath);                

                // update remote storage file content.
                if (stream != null)
                {
                    await stream.CopyToAsync(remoteStorageStream);
                    remoteStorageStream.SetLength(stream.Length);
                }
            }
            Logger.LogMessage("Sent to remote storage succesefully", UserFileSystemPath);
        }

        public Task CloseAsync(IOperationContext operationContext, IResultContext context)
        {
            throw new NotImplementedException();
        }
    }
}
