using ITHit.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Represents a basic information about the file or the folder in the user file system.
    /// In addition to properties provided by <see cref="IFileSystemItem"/> this class contains Etag property.
    /// </summary>
    public class FileSystemItemMetadataExt : IFileSystemItemMetadata
    {
        /// <inheritdoc/>
        public byte[] RemoteStorageItemId { get; set; }

        ///<inheritdoc/>
        public string Name { get; set; }

        ///<inheritdoc/>
        public FileAttributes Attributes { get; set; }

        ///<inheritdoc/>
        public DateTimeOffset CreationTime { get; set; }

        ///<inheritdoc/>
        public DateTimeOffset LastWriteTime { get; set; }

        ///<inheritdoc/>
        public DateTimeOffset LastAccessTime { get; set; }

        ///<inheritdoc/>
        public DateTimeOffset ChangeTime { get; set; }

        /// <summary>
        /// Lock info.
        /// </summary>
        /// <remarks>
        /// If the item is locked, this property contains info about the lock. 
        /// It is set to null otherwise.
        /// </remarks>
        public ServerLockInfo Lock { get; set; }

        /// <summary>
        /// Custom columns data to be displayed in the file manager.
        /// </summary>
        public IEnumerable<FileSystemItemPropertyData> CustomProperties { get; set; } = new FileSystemItemPropertyData[] { };
    }
}
