using System;
using System.IO;
using System.Threading.Tasks;
using FileProvider;
using Foundation;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;
using VirtualFilesystemCommon;

namespace FileProviderExtension
{
    [Register(nameof(VfsEngine))]
    public class VfsEngine : EngineMac
    {
        /// <summary>
        /// <see cref="ConsoleLogger"/> instance.
        /// </summary>
        private readonly ConsoleLogger logger;
        private RemoteStorageMonitor remoteStorageMonitor;

        [Export("initWithDomain:")]
        public VfsEngine(NSFileProviderDomain domain)
            : base(domain)
        {
            License = AppGroupSettings.GetLicense();
            logger = new ConsoleLogger(GetType().Name);

            remoteStorageMonitor = new RemoteStorageMonitor(AppGroupSettings.GetRemoteRootPath(), NSFileProviderManager.FromDomain(domain));
            remoteStorageMonitor.Start();
        }

        public override async Task<IFileSystemItem> GetFileSystemItemAsync(string path)
        {
            string remotePath = Mapping.MapPath(path);

            if (File.Exists(remotePath))
            {
                FileInfo fileInfo = new FileInfo(remotePath);
                return new VfsFile(Mapping.ReverseMapPath(fileInfo.FullName), fileInfo.Attributes, fileInfo.CreationTime, fileInfo.LastWriteTime, fileInfo.LastAccessTime, fileInfo.Length, logger);
            }
            else if (Directory.Exists(remotePath))
            {
                DirectoryInfo dirInfo = new DirectoryInfo(remotePath);
                return new VfsFolder(Mapping.ReverseMapPath(dirInfo.FullName), dirInfo.Attributes, dirInfo.CreationTime, dirInfo.LastWriteTime, dirInfo.LastAccessTime, logger);
            }

            return null;
        }

        public override void LogError(string message, string sourcePath = null, string targetPath = null, Exception ex = null)
        {
            logger.LogError(message, sourcePath, targetPath, ex);
        }

        public override void LogMessage(string message, string sourcePath = null, string targetPath = null)
        {
            logger.LogMessage(message, sourcePath, targetPath);
        }

        public override void RiseError(string message, string sourcePath = null, string targetPath = null, Exception ex = null)
        {
            logger.LogError(message, sourcePath, targetPath, ex);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        protected override void Stop()
        {
            remoteStorageMonitor.Stop();
        }
    }
}
