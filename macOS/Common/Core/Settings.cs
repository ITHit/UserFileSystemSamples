using System;
namespace Common.Core
{
    /// <summary>
    /// Strongly binded project settings.
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// IT Hit User File System license string.
        /// </summary>
        public string UserFileSystemLicense { get; set; }

        /// <summary>
        /// Your virtual file system will be mounted under this path.
        /// </summary>
        public string UserFileSystemRootPath { get; set; }

        /// <summary>
        /// Automatically lock the file in the remote storage when a file handle is being opened for writing, unlock on close.
        /// </summary>
        public bool AutoLock { get; set; }
    }
}

