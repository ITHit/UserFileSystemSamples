using System;
using Common.Core;

namespace VirtualFileSystemCommon
{
	public class AppSettings: Settings
	{
        /// <summary>
        /// Folder that contains file structure to simulate data for your virtual file system.
        /// </summary>
        /// <remarks>
        /// In your real-life application you will read data from your cloud storage, database or any other location, instead of this folder.
        /// </remarks>
        public string RemoteStorageRootPath { get; set; }
    }
}

