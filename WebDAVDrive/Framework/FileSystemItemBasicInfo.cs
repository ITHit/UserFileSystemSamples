﻿using ITHit.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VirtualFileSystem
{
    /// <summary>
    /// Represents a basic information about the file or the folder in the user file system.
    /// In addition to properties provided by <see cref="IFileSystemItem"/> this class contains Etag property.
    /// </summary>
    internal class FileSystemItemBasicInfo : IFileSystemItemBasicInfo
    {
        ///<inheritdoc/>
        public string Name { get; set; }

        ///<inheritdoc/>
        public FileAttributes Attributes { get; set; }

        ///<inheritdoc/>
        public byte[] CustomData { get; set; }

        ///<inheritdoc/>
        public DateTime CreationTime { get; set; }

        ///<inheritdoc/>
        public DateTime LastWriteTime { get; set; }

        ///<inheritdoc/>
        public DateTime LastAccessTime { get; set; }

        ///<inheritdoc/>
        public DateTime ChangeTime { get; set; }

        /// <summary>
        /// Server ETag.
        /// </summary>
        public string ETag { get; set; }

        /// <summary>
        /// Indicates if the item is locked by another user in the remote storage.
        /// </summary>
        public bool LockedByAnotherUser { get; set; }
    }
}
