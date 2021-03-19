using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using log4net;

using ITHit.FileSystem.Samples.Common;
using System.IO;

namespace VirtualFileSystem
{
    /// <inheritdoc/>
    internal class VirtualDrive : VirtualDriveBase
    {
        /// <inheritdoc/>
        public VirtualDrive(string license, string userFileSystemRootPath, ILog log, double syncIntervalMs) 
            : base(license, userFileSystemRootPath, log, syncIntervalMs)
        {

        }

        /// <inheritdoc/>
        public override async Task<IUserFileSystemItem> GetUserFileSystemItemAsync(string userFileSystemPath)
        {
            if (File.Exists(userFileSystemPath))
            {
                return new UserFile(userFileSystemPath);
            }
            if (Directory.Exists(userFileSystemPath))
            {
                return new UserFolder(userFileSystemPath);
            }

            // When a file handle is being closed during delete, the file does not exist, return null.
            return null;
        }
    }
}
