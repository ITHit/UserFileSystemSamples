using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;

namespace VirtualFileSystem
{
    /// <summary>
    /// Represents a file in the remote storage.
    /// </summary>
    /// <remarks>You will change methods of this class to read/write data from/to your remote storage.</remarks>
    internal class VirtualFile : VirtualFileSystemItem, IVirtualFile
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemFilePath">Path of this file in the user file system.</param>
        /// <param name="virtualDrive">Virtual Drive instance that created this item.</param>
        /// <param name="logger">Logger.</param>
        public VirtualFile(string userFileSystemFilePath, VirtualDrive virtualDrive, ILogger logger) 
            : base(userFileSystemFilePath, virtualDrive, logger)
        {

        }

        /// <summary>
        /// Reads this file content from the remote storage.
        /// </summary>
        /// <param name="offset">File content offset, in bytes, to start reading from.</param>
        /// <param name="length">Data length to read, in bytes.</param>
        /// <param name="fileSize">Total file size, in bytes.</param>
        /// <param name="resultContext">
        /// You will use this parameter to return file content by 
        /// calling <see cref="ITransferDataResultContext.ReturnData(byte[], long, long)"/>
        /// </param>
        public async Task ReadAsync(long offset, long length, long fileSize, ITransferDataResultContext resultContext)
        {
            // On Windows this method has a 60 sec timeout. 
            // To process longer requests and reset the timout timer call the resultContext.ReportProgress() or resultContext.ReturnData() method.

            await using (FileStream stream = File.OpenRead(RemoteStoragePath))
            {
                const long MAX_CHUNK_SIZE = 0x500000; //5Mb

                long chunkSize = Math.Min(MAX_CHUNK_SIZE, length);

                stream.Seek(offset, SeekOrigin.Begin);

                long total = offset + length;
                byte[] buffer = new byte[chunkSize];
                long bytesRead;
                while ( (bytesRead = await stream.ReadAsync(buffer, 0, (int)chunkSize) ) > 0)
                {
                    resultContext.ReturnData(buffer, offset, bytesRead);
                    offset += bytesRead;
                    length -= bytesRead;
                    chunkSize = Math.Min(MAX_CHUNK_SIZE, length);
                    if (offset >= total)
                    {
                        return;
                    }
                }
            }
        }


        public async Task<bool> ValidateDataAsync(long offset, long length)
        {
            return true;
        }

        /// <summary>
        /// Updates file in the remote storage.
        /// </summary>
        /// <param name="fileInfo">New information about the file, such as creation date, modification date, attributes, etc.</param>
        /// <param name="content">New file content or null if the file content is not modified.</param>
        /// <param name="eTagOld">The ETag to be sent to the remote storage as part of the update request to make sure the content is not overwritten.</param>
        /// <param name="lockInfo">Information about the lock. Caller passes null if the item is not locked.</param>
        /// <returns>The new ETag returned from the remote storage.</returns>
        public async Task<string> UpdateAsync(IFileMetadata fileInfo, Stream content = null, string eTagOld = null, ServerLockInfo lockInfo = null)
        {
            return await CreateOrUpdateFileAsync(RemoteStoragePath, fileInfo, FileMode.Open, content, eTagOld, lockInfo);
        }
    }
}
