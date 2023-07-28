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
        private WebDavSession webDavSession;

        [Export("initWithDomain:")]
        public VirtualEngine(NSFileProviderDomain domain)
            : base(domain)
        {
            License = AppGroupSettings.GetLicense();
            ConsoleLogger consolelogger = new ConsoleLogger(GetType().Name);
   
            Error += consolelogger.LogError;
            Message += consolelogger.LogMessage;
            Debug += consolelogger.LogDebug;
           
            webDavSession = new WebDavSession(AppGroupSettings.GetWebDAVClientLicense());
            webDavSession.CustomHeaders.Add("InstanceId", Environment.MachineName);

            // set remote root storage item id.
            SetRemoteStorageRootItemId(GetRootStorageItemIdAsync().Result);
          
            Logger.LogMessage("Engine started.");
        }

        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(byte[] remoteStorageItemId, FileSystemItemType itemType, IContext context, ILogger logger = null)
        {
            if (itemType == FileSystemItemType.File)
            {
                return new VirtualFile(remoteStorageItemId, webDavSession, Logger);
            }
            else
            {
                return new VirtualFolder(remoteStorageItemId, webDavSession, Logger);
            }
        }

        
        /// <inheritdoc/>
        public override async Task<IMenuCommand> GetMenuCommandAsync(Guid menuGuid, IOperationContext operationContext)
        {
            // For this method to be called you need to register a menu command handler.
            // See method description for more details.

            Logger.LogDebug($"{nameof(IEngine)}.{nameof(GetMenuCommandAsync)}()", menuGuid.ToString());

            Guid actionCommandGuid = typeof(ActionMenuCommand).GUID;

            if (menuGuid == actionCommandGuid)
            {
                return new ActionMenuCommand(this, Logger);
            }

            Logger.LogError($"Menu not found", menuGuid.ToString());

            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns remote storage item id.
        /// </summary>
        /// <returns></returns>
        public async Task<byte[]> GetRootStorageItemIdAsync()
        {
            return (await new VirtualFolder(Encoding.UTF8.GetBytes(AppGroupSettings.GetWebDAVServerUrl()), webDavSession, Logger).GetMetadataAsync()).RemoteStorageItemId;
        }

                

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            webDavSession.Dispose();
        }

        protected override void Stop()
        {
            Logger.LogMessage($"{nameof(IEngine)}.{nameof(Stop)}()");
        }
    }
}
