using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using AuthenticationServices;
using Common.Core;
using FileProvider;
using Foundation;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;
using ITHit.FileSystem.Mac.Contexts;
using ITHit.WebDAV.Client;
using WebDAVCommon;

// workaround for NSFileProvider Extension crash on MacOS 14.4 https://github.com/xamarin/xamarin-macios/issues/20034
[assembly: ObjCRuntime.LinkWith(LinkerFlags = "-Wl,-unexported_symbol -Wl,_main")]

namespace WebDAVFileProviderExtension
{
    [Register(nameof(VirtualEngine))]
    public class VirtualEngine : EngineMac
    {
        /// <summary>
        /// WebDAV session.
        /// </summary>
        public WebDavSession WebDavSession;

        /// <summary>
        /// Secure Storage.
        /// </summary>
        public SecureStorage SecureStorage;

        /// <summary>
        /// WebDAV server root url.
        /// </summary>
        public readonly string WebDAVServerUrl;

        /// <summary>
        /// Automatic lock timout in milliseconds.
        /// </summary>
        public readonly double AutoLockTimoutMs;

        /// <summary>
        /// Manual lock timout in milliseconds.
        /// </summary>
        public readonly double ManualLockTimoutMs;

        /// <summary>
        /// Currently loged-in user name or user ID. 
        /// </summary>
        /// <remarks>
        /// Used to set lock Owner name as well as to distinguish locks applied
        /// by the currently loged-in user from locks applied by other users, across multiple devices.
        /// The default value of the Environment.UserName is used for demo purposes only.
        /// </remarks>
        public string CurrentUserPrincipal { get; set; } = Environment.UserName;

        [Export("initWithDomain:")]
        public VirtualEngine(NSFileProviderDomain domain)
            : base(domain)
        {
            License = AppGroupSettings.Settings.Value.UserFileSystemLicense;
            ConsoleLogger consolelogger = new ConsoleLogger(GetType().Name);

            Error += consolelogger.LogError;
            Message += consolelogger.LogMessage;
            Debug += consolelogger.LogDebug;

            SecureStorage = new SecureStorage(domain.Identifier);

            // Get WebDAV url from user settings.
            WebDAVServerUrl = SecureStorage.GetAsync(domain.Identifier, false).Result;

            AutoLock = AppGroupSettings.Settings.Value.AutoLock;
            AutoLockTimoutMs = AppGroupSettings.Settings.Value.AutoLockTimoutMs;
            ManualLockTimoutMs = AppGroupSettings.Settings.Value.ManualLockTimoutMs;

            // Init WebDAV session.
            WebDavSession = WebDavSessionUtils.GetWebDavSessionAsync(SecureStorage).Result;

            // set remote root storage item id.
            SetRemoteStorageRootItemId(GetRootStorageItemIdAsync().Result);            

            Logger.LogMessage("Engine started.");
        }

        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(byte[] remoteStorageItemId, FileSystemItemType itemType, IContext context, ILogger logger = null)
        {
            if (itemType == FileSystemItemType.File)
            {
                return new VirtualFile(remoteStorageItemId, this, Logger);
            }
            else
            {
                return new VirtualFolder(remoteStorageItemId, this, Logger);
            }
        }

        
        /// <inheritdoc/>
        public override async Task<IMenuCommand> GetMenuCommandAsync(Guid menuGuid, IOperationContext operationContext)
        {
            // For this method to be called you need to register a menu command handler.
            // See method description for more details.

            Logger.LogDebug($"{nameof(IEngine)}.{nameof(GetMenuCommandAsync)}()", menuGuid.ToString());
            Dictionary<Guid, IMenuCommand> menuCommands = new Dictionary<Guid, IMenuCommand>() {
                { typeof(UnLockMenuCommand).GUID, new UnLockMenuCommand(this, Logger) }, { typeof(LockMenuCommand).GUID, new LockMenuCommand(this, Logger) } };

            if (menuCommands.ContainsKey(menuGuid))
            {
                return menuCommands[menuGuid];
            }

            Logger.LogError($"Menu not found", menuGuid.ToString());

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override async Task<byte[]> GetRootStorageItemIdAsync()
        {
            Logger.LogMessage($"{nameof(VirtualEngine)}.{nameof(GetRootStorageItemIdAsync)}()");
            try
            {
                return (await new VirtualFolder(Encoding.UTF8.GetBytes(WebDAVServerUrl), this, Logger).GetMetadataAsync())?.RemoteStorageItemId;
            }            
            catch (Exception ex)
            {
                Logger.LogError($"{nameof(VirtualEngine)}.{nameof(GetRootStorageItemIdAsync)}()", ex: ex);
                return null;
            }
        }

                

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            WebDavSession?.Dispose();
        }

        protected override void Stop()
        {
            Logger.LogMessage($"{nameof(IEngine)}.{nameof(Stop)}()");
        }

        public override async Task<bool> IsAuthenticatedAsync()
        {
            Logger.LogMessage($"{nameof(IEngine)}.{nameof(IsAuthenticatedAsync)}()");
            string requireAuthentication = await SecureStorage.GetAsync("RequireAuthentication");
            string updateWebDavSession = await SecureStorage.GetAsync("UpdateWebdavSession");

            // Check if Authentication is still required.
            if (!string.IsNullOrEmpty(requireAuthentication) && !string.IsNullOrEmpty(updateWebDavSession))
            {
                // Update auth for WebDav session.
                Logger.LogMessage($"{nameof(IEngine)}.{nameof(IsAuthenticatedAsync)}() - update WebDav session.");

                await WebDavSessionUtils.UpdateAuthenticationAsync(WebDavSession, SecureStorage);
                await SecureStorage.SetAsync("UpdateWebdavSession", "");

                if (await GetRootStorageItemIdAsync() != null)
                {
                    requireAuthentication = string.Empty;
                    await SecureStorage.SetAsync("RequireAuthentication", "");
                }
            }

            return string.IsNullOrEmpty(requireAuthentication);
        }
    }
}
