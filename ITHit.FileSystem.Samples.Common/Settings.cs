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
        /// Full synchronization interval in milliseconds.
        /// </summary>
        public double SyncIntervalMs { get; set; }

        /// <summary>
        /// Network delay in milliseconds. When this parameter is > 0 the file download will be delayd to demonstrate file transfer progress.
        /// Set this parameter to 0 to avoid any network simulation delays.
        /// </summary>
        public int NetworkSimulationDelayMs { get; set; }

        /// <summary>
        /// Path to the icons folder.
        /// </summary>
        public string IconsFolderPath { get; set; }

        /// <summary>
        /// Product name. Displayed as a mounted folder name under Desktop as 
        /// well in every location where product name is required.
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// Path to the folder that stores ETags, locks and other data associated with files and folders.
        /// </summary>
        public string ServerDataFolderPath { get; set; }

        /// <summary>
        /// Automatically lock the file in remote storage when a file handle is being opened for writing, unlock on close.
        /// </summary>
        public bool AutoLock { get; set; }
    }
}
