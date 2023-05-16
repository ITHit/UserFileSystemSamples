using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Common.Core;
using FileProvider;
using Foundation;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;
using ITHit.WebDAV.Client;
using WebDAVCommon;

namespace WebDAVFileProviderExtension
{
    [Register(nameof(VirtualEngine))]
    public class VirtualEngine : EngineMac
    {
        /// <summary>
        /// <see cref="ConsoleLogger"/> instance.
        /// </summary>
        private readonly ConsoleLogger logger;
        private WebDavSession webDavSession;

        [Export("initWithDomain:")]
        public VirtualEngine(NSFileProviderDomain domain)
            : base(domain)
        {
            License = AppGroupSettings.GetLicense();
            logger = new ConsoleLogger(GetType().Name);
            logger.LogMessage("Engine started.");
            webDavSession = new WebDavSession(AppGroupSettings.GetWebDAVClientLicense(), new NSUrlSessionHandler());
            webDavSession.CustomHeaders.Add("InstanceId", Environment.MachineName);
            // set remote root storage item id.
            SetRemoteStorageRootItemId(GetRootStorageItemIdAsync().Result);
        }

        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(string userFileSystemPath, FileSystemItemType itemType, byte[] remoteStorageItemId = null, ILogger logger = null)
        {
            string remoteStorageUrl = Encoding.UTF8.GetString(remoteStorageItemId);
            logger.LogMessage($"{nameof(IEngine)}.{nameof(GetFileSystemItemAsync)}()", userFileSystemPath);

            if (itemType == FileSystemItemType.File)
            {
                return new VirtualFile(remoteStorageUrl, webDavSession, this);
            }
            else
            {
                return new VirtualFolder(remoteStorageUrl, webDavSession, this);
            }
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

        /// <summary>
        /// Returns remote storage item id.
        /// </summary>
        /// <returns></returns>
        public async Task<byte[]> GetRootStorageItemIdAsync()
        {
            return (await new VirtualFolder(AppGroupSettings.GetWebDAVServerUrl(), webDavSession, this).GetMetadataAsync()).RemoteStorageItemId;
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
            webDavSession.Dispose();
        }

        protected override void Stop()
        {
        }
    }
}
