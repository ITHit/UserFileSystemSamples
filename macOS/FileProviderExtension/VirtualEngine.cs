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
    [Register(nameof(VirtualEngine))]
    public class VirtualEngine : EngineMac
    {
        /// <summary>
        /// <see cref="ConsoleLogger"/> instance.
        /// </summary>
        private readonly ConsoleLogger logger;
        private RemoteStorageMonitor remoteStorageMonitor;

        [Export("initWithDomain:")]
        public VirtualEngine(NSFileProviderDomain domain)
            : base(domain)
        {
            License = AppGroupSettings.GetLicense();
            logger = new ConsoleLogger(GetType().Name);

            remoteStorageMonitor = new RemoteStorageMonitor(AppGroupSettings.GetRemoteRootPath(), NSFileProviderManager.FromDomain(domain));
            remoteStorageMonitor.Start();
        }

        public override async Task<IFileSystemItem> GetFileSystemItemAsync(string path, FileSystemItemType itemType)
        {
            string remotePath = Mapping.MapPath(path);
            logger.LogMessage($"{nameof(IEngine)}.{nameof(GetFileSystemItemAsync)}()", path, remotePath);

            if (File.Exists(remotePath))
            {
                return new VirtualFile(path, this);
            }
            else if (Directory.Exists(remotePath))
            {
                return new VirtualFolder(path, this);
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
