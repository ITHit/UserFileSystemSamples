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

            SecureStorage = new SecureStorage();

            WebDAVServerUrl = AppGroupSettings.Settings.Value.WebDAVServerUrl;
            DomainSettings domainSettings = SecureStorage.GetAsync<DomainSettings>(domain.Identifier).Result;
            if (domainSettings != null && !string.IsNullOrEmpty(domainSettings.WebDAVServerUrl))
            {
                WebDAVServerUrl = domainSettings.WebDAVServerUrl;
            }

            InitWebDavSession();

            AutoLock = AppGroupSettings.Settings.Value.AutoLock;
            AutoLockTimoutMs = AppGroupSettings.Settings.Value.AutoLockTimoutMs;
            ManualLockTimoutMs = AppGroupSettings.Settings.Value.ManualLockTimoutMs;

            // set remote root storage item id.
            SetRemoteStorageRootItemId(GetRootStorageItemIdAsync().Result);
          
            Logger.LogMessage("Engine started.");
        }

        /// <summary>
        /// Initializes WebDAV session.
        /// </summary>
        internal void InitWebDavSession()
        {
            if(WebDavSession != null)
            {
                WebDavSession.Dispose();
            }
            WebDavSession = new WebDavSession(AppGroupSettings.Settings.Value.WebDAVClientLicense);
            WebDavSession.CustomHeaders.Add("InstanceId", Environment.MachineName);

            string loginType = SecureStorage.GetAsync("LoginType").Result;
            if (!string.IsNullOrEmpty(loginType) && loginType.Equals("UserNamePassword"))
            {
                WebDavSession.Credentials = new NetworkCredential(SecureStorage.GetAsync("UserName").Result, SecureStorage.GetAsync("Password").Result);
            }
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

            if(menuCommands.ContainsKey(menuGuid))
            {
                return menuCommands[menuGuid];
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
            Logger.LogMessage($"{nameof(VirtualEngine)}.{nameof(GetRootStorageItemIdAsync)}()");
            try
            {                
                return (await new VirtualFolder(Encoding.UTF8.GetBytes(WebDAVServerUrl), this, Logger).GetMetadataAsync())?.RemoteStorageItemId;
            }
            catch (ITHit.WebDAV.Client.Exceptions.WebDavHttpException httpException)
            {
                Logger.LogError($"{nameof(VirtualEngine)}.{nameof(GetRootStorageItemIdAsync)}()", ex: httpException);
                switch (httpException.Status.Code)
                {
                    // Challenge-responce auth: Basic, Digest, NTLM or Kerberos
                    case 401:
                        // Set login type to display sing in button in Finder.
                        await SecureStorage.RequireAuthenticationAsync();
                        return null;
                }
                return null;
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
            WebDavSession.Dispose();
        }

        protected override void Stop()
        {
            Logger.LogMessage($"{nameof(IEngine)}.{nameof(Stop)}()");
        }

        public override async Task<bool> IsAuthenticatedAsync()
        {
            Logger.LogMessage($"{nameof(IEngine)}.{nameof(IsAuthenticatedAsync)}()");
            string loginType = await SecureStorage.GetAsync("LoginType");
            return string.IsNullOrEmpty(loginType) || loginType != "RequireAuthentication";
        }
    }
}
