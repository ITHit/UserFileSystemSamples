using System;
using System.IO;
using System.Runtime.CompilerServices;
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

        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(string userFileSystemPath, FileSystemItemType itemType, byte[] remoteStorageItemId = null, ILogger logger = null)
        {
            string remotePath = Mapping.MapPath(userFileSystemPath);
            logger.LogMessage($"{nameof(IEngine)}.{nameof(GetFileSystemItemAsync)}()", userFileSystemPath, remotePath);

            if (File.Exists(remotePath))
            {
                return new VirtualFile(userFileSystemPath, this);
            }
            else if (Directory.Exists(remotePath))
            {
                return new VirtualFolder(userFileSystemPath, this);
            }

            return null;
        }


        
        /// <inheritdoc/>
        public override async Task<IMenuCommand> GetMenuCommandAsync(Guid menuGuid)
        {
            // For this method to be called you need to register a menu command handler.
            // See method description for more details.

            logger.LogDebug($"{nameof(IEngine)}.{nameof(GetMenuCommandAsync)}()", menuGuid.ToString());

            Guid actionCommandGuid = typeof(ActionMenuCommand).GUID;

            if (menuGuid == actionCommandGuid)
            {
                return new ActionMenuCommand(this, this.logger);
            }

            logger.LogError($"Menu not found", menuGuid.ToString());
            throw new NotImplementedException();
        }
        

        public override void LogDebug(string message, string sourcePath = null, string targetPath = null, IOperationContext operationContext = null, [CallerLineNumber] int callerLineNumber = 0, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null)
        {
            logger.LogDebug(message, sourcePath, targetPath);
        }

        /// <inheritdoc/>
        public override void LogError(string message, string sourcePath = null, string targetPath = null, Exception ex = null, IOperationContext operationContext = null, [CallerLineNumber] int callerLineNumber = 0, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null)
        {
            logger.LogError(message, sourcePath, targetPath, ex);
        }

        /// <inheritdoc/>
        public override void LogMessage(string message, string sourcePath = null, string targetPath = null, IOperationContext operationContext = null, [CallerLineNumber] int callerLineNumber = 0, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null)
        {
            logger.LogMessage(message, sourcePath, targetPath);
        }

        public override void RiseError(string message, string sourcePath = null, string targetPath = null, Exception ex = null, IOperationContext operationContext = null, [CallerLineNumber] int callerLineNumber = 0, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null)
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
