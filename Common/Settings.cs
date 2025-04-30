using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Strongly binded project settings.
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// Unique ID of this application.
        /// </summary>
        /// <remarks>
        /// If you must to run more than one instance of this application side-by-side on the same client machine
        /// (aka Corporate Drive and Personal Drive) set unique ID for each instance.
        /// </remarks>
        public string AppID { get; set; }

        /// <summary>
        /// IT Hit User File System license string.
        /// </summary>
        public string UserFileSystemLicense { get; set; }

        /// <summary>
        /// Your virtual file system will be mounted under this path.
        /// </summary>
        public string UserFileSystemRootPath { get; set; }

        /// <summary>
        /// Path to the icons folder.
        /// </summary>
        public string IconsFolderPath { get; set; }

        /// <summary>
        /// Product name. Displayed in every location where product name is required.
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// Automatically lock the file in the remote storage when a file handle is being opened for writing, unlock on close.
        /// </summary>
        public bool AutoLock { get; set; }

        /// <summary>
        /// The folder content invalidation period in milliseconds. 
        /// </summary>
        public double FolderInvalidationIntervalMs { get; set; }

        /// <summary>
        /// Get or set the custom columns for the file system items.
        /// </summary>
        /// <remarks>
        /// Each custom column is represented by an instance of the <see cref="CustomColumn"/> class, 
        /// which includes an ID and a Name.
        /// </remarks>
        public Dictionary<int, string> CustomColumns { get; set; }
    }
}
