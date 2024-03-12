using System;
using System.IO;

using ITHit.FileSystem;
using ITHit.FileSystem.Synchronization;


namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Represents a basic information about the file or the folder in the user file system.
    /// In addition to the properties provided by <see cref="IFileSystemItemMetadata"/> provides <see cref="Lock"/> property.
    /// </summary>
    public class FileSystemItemMetadataExt : FileSystemItemMetadata
    {
        /// <summary>
        /// Lock info or null if the item is not locked.
        /// </summary>
        public ServerLockInfo Lock { get; set; }
    }
}
