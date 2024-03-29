using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FileProvider;
using Foundation;

using ITHit.FileSystem;
using ITHit.FileSystem.Mac;

using Common.Core;
using System.Text;
using VirtualFileSystemCommon;

namespace FileProviderExtension
{
    [Register(nameof(VirtualEngine))]
    public class VirtualEngine : EngineMac
    {
        [Export("initWithDomain:")]
        public VirtualEngine(NSFileProviderDomain domain)
            : base(domain)
        {
            License = AppGroupSettings.Settings.Value.UserFileSystemLicense;
            ConsoleLogger consolelogger = new ConsoleLogger(GetType().Name);
            Error += consolelogger.LogError;
            Message += consolelogger.LogMessage;
            Debug += consolelogger.LogDebug;

            AutoLock = AppGroupSettings.Settings.Value.AutoLock;

            SecureStorage secureStorage = new SecureStorage();

            DomainSettings domainSettings = secureStorage.GetAsync<DomainSettings>(domain.Identifier).Result;
            string remoteStorageRootPath = AppGroupSettings.Settings.Value.RemoteStorageRootPath;

            if (domainSettings != null && !string.IsNullOrEmpty(domainSettings.RemoteStorageRootPath))
            {
                remoteStorageRootPath = domainSettings.RemoteStorageRootPath;
            }

            // set remote root storage item id.
            SetRemoteStorageRootItemId(Mapping.EncodePath(remoteStorageRootPath));

            Logger.LogMessage($"Engine started.");
        }

        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(byte[] remoteStorageItemId, FileSystemItemType itemType, IContext context, ILogger logger = null)
        {
            string remotePath = Mapping.DecodePath(remoteStorageItemId);
            Logger.LogMessage($"{nameof(IEngine)}.{nameof(GetFileSystemItemAsync)}()", remotePath);

            if (File.Exists(remotePath))
            {
                return new VirtualFile(remotePath, Logger);
            }
            else if (Directory.Exists(remotePath))
            {
                return new VirtualFolder(remotePath, Logger);
            }

            return null;
        }


        
        /// <inheritdoc/>
        public override async Task<IMenuCommand> GetMenuCommandAsync(Guid menuGuid, IOperationContext operationContext)
        {
            // For this method to be called you need to register a menu command handler.
            // See method description for more details.

            Logger.LogDebug($"{nameof(IEngine)}.{nameof(GetMenuCommandAsync)}({menuGuid.ToString()})");

            Guid actionCommandGuid = typeof(ActionMenuCommand).GUID;

            if (menuGuid == actionCommandGuid)
            {
                return new ActionMenuCommand(this, this.Logger);
            }

            Logger.LogError($"Menu not found", menuGuid.ToString());
            throw new NotImplementedException();
        }
        

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        protected override void Stop()
        {
        }
    }
}
