using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using log4net;

using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem;

namespace WebDAVDrive
{
    /// <summary>
    /// Processes OS file system calls, 
    /// synchronizes the user file system to the remote storage and back, 
    /// monitors files pinning and unpinning in the local file system,
    /// monitores changes in the remote storage.
    /// </summary>
    internal class VirtualDrive : VirtualDriveBase
    {
        /// <summary>
        /// Monitors changes in the remote storage, notifies the client and updates the user file system.
        /// </summary>
        private readonly RemoteStorageMonitor remoteStorageMonitor;

        /// <inheritdoc/>
        public VirtualDrive(string license, string userFileSystemRootPath, Settings settings, ILog log) 
            : base(license, userFileSystemRootPath, settings, log)
        {
            string remoteStorageRootPath = Mapping.MapPath(userFileSystemRootPath);
            remoteStorageMonitor = new RemoteStorageMonitor(remoteStorageRootPath, this, log);
        }

        /// <inheritdoc/>
        public override async Task<IVirtualFileSystemItem> GetVirtualFileSystemItemAsync(string userFileSystemPath, FileSystemItemTypeEnum itemType, ILogger logger)
        {
            if (itemType == FileSystemItemTypeEnum.File)
            {
                return new VirtualFile(userFileSystemPath, logger);
            }
            else
            {
                return new VirtualFolder(userFileSystemPath, logger);
            }
        }

        /// <summary>
        /// Enables or disables full synchronization service, user file sytem monitor and remote storage monitor.
        /// </summary>
        /// <param name="enabled">Pass true to start synchronization. Pass false - to stop.</param>
        public override async Task SetEnabledAsync(bool enabled)
        {
            await base.SetEnabledAsync(enabled);

            if(enabled)
            {
                await remoteStorageMonitor.StartAsync();
            }
            else
            {
                await remoteStorageMonitor.StopAsync();
            }
        }


        private bool disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    remoteStorageMonitor.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
            base.Dispose(disposing);
        }
    }
}
