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
        [Export("initWithDomain:")]
        public VfsEngine(NSFileProviderDomain domain)
            : base(domain)
        {
            License = AppGroupSettings.GetLicense();
        }

        public override async Task<IFileSystemItem> GetFileSystemItemAsync(string path)
        {
            string remotePath = Mapping.MapPath(path);

            if (File.Exists(remotePath))
            {
                FileInfo fileInfo = new FileInfo(remotePath);
                return new VfsFile(Mapping.ReverseMapPath(fileInfo.FullName), fileInfo.Attributes, fileInfo.CreationTime, fileInfo.LastWriteTime, fileInfo.LastAccessTime, fileInfo.Length);
            }

            if (Directory.Exists(remotePath))
            {
                DirectoryInfo dirInfo = new DirectoryInfo(remotePath);
                return new VfsFolder(Mapping.ReverseMapPath(dirInfo.FullName), dirInfo.Attributes, dirInfo.CreationTime, dirInfo.LastWriteTime, dirInfo.LastAccessTime);
            }

            return null;
        }

        public override void LogError(string message, string sourcePath = null, string targetPath = null, Exception ex = null)
        {
            throw new NotImplementedException();
        }

        public override void LogMessage(string message, string sourcePath = null, string targetPath = null)
        {
            throw new NotImplementedException();
        }

        public override void RiseError(string message, string sourcePath = null, string targetPath = null, Exception ex = null)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
