using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Synchronization;
using ITHit.FileSystem.Windows;
using ITHit.WebDAV.Client;
using ITHit.WebDAV.Client.Exceptions;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WebDAVDrive.Dialogs;
using WebDAVDrive.Enums;
using WebDAVDrive.Services;
using WinUIEx;
using InvalidLicenseException = ITHit.FileSystem.InvalidLicenseException;

namespace WebDAVDrive
{
    /// <inheritdoc />
    public class VirtualEngine : VirtualEngineBase
    {
        /// <summary>
        /// Application settings.
        /// </summary>
        public AppSettings Settings;

        /// <summary>
        /// WebDAV client for accessing the WebDAV server.
        /// </summary>
        public readonly WebDavSession DavClient;

        /// <summary>
        /// Engine instance ID, unique for every Engine instance.
        /// </summary>
        /// <remarks>
        /// Used to prevent circular calls between remote storage and user file system.
        /// You will send this ID with every update to the remote storage, 
        /// so you remote storage do not send updates to the client that initiated the change.
        /// </remarks>
        public readonly Guid InstanceId = Guid.NewGuid();

        /// <summary>
        /// Monitors changes in the remote storage, notifies the client and updates the user file system.
        /// </summary>
        public RemoteStorageMonitorBase RemoteStorageMonitor;

        /// <summary>
        /// Maps remote storage path to the user file system path and vice versa. 
        /// </summary>
        public readonly Mapping Mapping;

        /// <summary>
        /// Commands.
        /// </summary>
        public readonly Commands Commands;

        /// <summary>
        /// Automatic lock timeout in milliseconds.
        /// </summary>
        public double AutoLockTimeoutMs;

        /// <summary>
        /// Domains service.
        /// </summary>
        private readonly DrivesService domainsService;

        /// <summary>
        /// Manual lock timeout in milliseconds.
        /// </summary>
        public double ManualLockTimeoutMs;

        /// <summary>
        /// Controls the number of events in the tray window.
        /// </summary>
        public int TrayMaxHistoryItems;

        /// <summary>
        /// Maximum number of login attempts.
        /// </summary>
        private readonly uint loginRetriesMax = 3;

        /// <summary>
        /// Current login attempt.
        /// </summary>
        private uint loginRetriesCurrent = 0;

        /// <summary>
        /// Logger for UI.
        /// </summary>
        private log4net.ILog log;

        /// <summary>
        /// Displayed in UI.
        /// </summary>
        private readonly string productName;

        /// <summary>
        /// Unique application ID.
        /// </summary>
        private readonly string appId;

        /// <summary>
        /// Secure storage service.
        /// </summary>
        private readonly SecureStorageService secureStorage;

        /// <summary>
        /// Remote storage root path.
        /// </summary>
        internal readonly string RemoteStorageRootPath;

        /// <summary>
        /// Web sockets server that sends notifications about changes on the server.
        /// </summary>
        private readonly string webSocketServerUrl;

        /// <summary>
        /// Title to be displayed in UI.
        /// </summary>
        internal string Title
        {
            get { return $"{productName} - {RemoteStorageRootPath}"; }
        }

        /// <summary>
        /// Storage key under which credentials are stored.
        /// </summary>
        private string CredentialsStorageKey
        {
            get { return $"{appId} - {RemoteStorageRootPath}"; }
        }

        public bool DavClientCredentialsSet
        {
            //ASP.NET Core Identity uses cookie with name .AspNetCore.Identity.Application, so check for it
            get { return DavClient.Credentials != null || DavClient.CookieContainer.GetAllCookies().Any(c => c.Name?.Equals(".aspnetcore.identity.application", StringComparison.InvariantCultureIgnoreCase) ?? false); }
        }

        /// <summary>
        /// Indicates whether the authentication was successful.
        /// </summary>
        private bool isAuthenticateSucceeded = false;

        /// <summary>
        /// Event fired when the authentication status changes.
        /// </summary>
        /// <remarks>See <see cref="EngineAuthentificationStatus"/> for statuses list.</remarks>
        public event Action<Engine, EngineAuthentificationStatus> LoginStatusChanged;


        /// <summary>
        /// Creates a vitual file system Engine.
        /// </summary>
        /// <param name="userFileSystemRootPath">
        /// A root folder of your user file system. 
        /// Your file system tree will be located under this folder.
        /// </param>
        /// <param name="remoteStorageRootPath">Path to the remote storage root.</param>
        /// <param name="webSocketServerUrl">Web sockets server that sends notifications about changes on the server.</param>
        /// <param name="logFormatter">Formats log output.</param>
        public VirtualEngine(
            string userFileSystemRootPath,
            string remoteStorageRootPath,
            string webSocketServerUrl,
            SecureStorageService secureStorageService,
            DrivesService domainsService,
            LogFormatter logFormatter,
            AppSettings appSettings)
            : base(appSettings.UserFileSystemLicense, userFileSystemRootPath, remoteStorageRootPath,
                  appSettings.IconsFolderPath, appSettings.SetLockReadOnly, logFormatter)
        {
            Settings = appSettings;
            productName = appSettings.ProductName;
            appId = appSettings.AppID;
            RemoteStorageRootPath = remoteStorageRootPath;
            this.webSocketServerUrl = webSocketServerUrl;
            secureStorage = secureStorageService;
            log = logFormatter.Log;
            this.domainsService = domainsService;

            Mapping = new Mapping(Path, remoteStorageRootPath);

            //set four main settings from secure storage, and in case storage does nto have them - take defaults from Settings
            UserSettingsService userSettingsService = ServiceProvider.GetService<UserSettingsService>();
            UserSettings? userSettings = userSettingsService.GetSettings(remoteStorageRootPath);
            AutoLockTimeoutMs = userSettings?.AutomaticLockTimeout ?? Settings.AutoLockTimeoutMs;
            ManualLockTimeoutMs = userSettings?.ManualLockTimeout ?? Settings.ManualLockTimeoutMs;
            SetLockReadOnly = userSettings?.SetLockReadOnly ?? Settings.SetLockReadOnly;
            AutoLock = userSettings?.AutoLock ?? Settings.AutoLock;
            TrayMaxHistoryItems = userSettings?.TrayMaxHistoryItems > 0 ? userSettings.TrayMaxHistoryItems : Settings.TrayMaxHistoryItems;

            DavClient = CreateWebDavSession(InstanceId);

            Commands = new Commands(this, remoteStorageRootPath, logFormatter.Log);

            Error += VirtualEngine_Error;
        }

        private void VirtualEngine_Error(IEngine sender, EngineErrorEventArgs e)
        {
            if (e.Exception is InvalidLicenseException)
            {
                domainsService.NotificationService.ShowLicenseError((InvalidLicenseException)e.Exception);
            }
        }

        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(byte[] remoteStorageId, FileSystemItemType itemType, IContext context, ILogger logger = null)
        {
            string userFileSystemPath = context.FileNameHint;
            if (itemType == FileSystemItemType.File)
            {
                return new VirtualFile(remoteStorageId, userFileSystemPath, this, AutoLockTimeoutMs, ManualLockTimeoutMs, Settings, logger);
            }
            else
            {
                return new VirtualFolder(remoteStorageId, userFileSystemPath, this, AutoLockTimeoutMs, ManualLockTimeoutMs, Settings, logger);
            }
        }

        public override async Task StartAsync(bool processChanges = true, CancellationToken cancellationToken = default)
        {
            if (!await AuthenticateAsync(null, cancellationToken, true))
            {
                LoginStatusChanged(this, EngineAuthentificationStatus.LoggedOut);
                //Authentication failed. There is no way to send requests without auth info. The Engine can not be started.
                return;
            }

            await InitAsync(cancellationToken);

            Logger.LogMessage($"Sync mode: {Settings.IncomingSyncMode}", Path);

            await base.StartAsync(processChanges, cancellationToken);
            LoginStatusChanged(this, DavClientCredentialsSet ? EngineAuthentificationStatus.LoggedIn : EngineAuthentificationStatus.Anonymous);

            // Create and start monitor, depending on server capabilities and prefered SyncMode.
            RemoteStorageMonitor = await StartRemoteStorageMonitorAsync(Settings.IncomingSyncMode, cancellationToken);

            Logger.LogMessage($"Actual sync mode: {SyncService.IncomingSyncMode}", Path);
        }

        public override async Task<bool> AuthenticateAsync(IItemsChange itemsChange, CancellationToken cancellationToken)
        {
            return await AuthenticateAsync(itemsChange, cancellationToken, false);
        }

        public async Task<bool> AuthenticateAsync(IItemsChange itemsChange, CancellationToken cancellationToken, bool skipCachedCredential)
        {
            if (await IsAuthenticatedAsync(itemsChange, cancellationToken) && !skipCachedCredential)
            {
                return true;
            }

            bool authenticated = true;
            try
            {
                // This call is only requitred to get server authentication type: Token-based, Basic, NTLM, Cookies, etc.
                await DavClient.GetFolderAsync(RemoteStorageRootPath, Mapping.GetDavProperties(), cancellationToken: cancellationToken);
            }
            catch (WebDavHttpException ex)
            {
                // Show login dialog or read auth info from storage.
                authenticated = ShowLoginDialog(ex, Path);
            }

            if (!authenticated)
            {
                ILogger logger = Logger.CreateLogger("WebDAV Session");
                logger.LogMessage("Authentication failed", RemoteStorageRootPath);
            }

            // Save authentication result.
            isAuthenticateSucceeded = authenticated;

            return authenticated;
        }

        public override async Task<bool> IsAuthenticatedAsync(IItemsChange itemsChange, CancellationToken cancellationToken)
        {
            return isAuthenticateSucceeded;
        }

        public async Task LogoutAsync()
        {
            DavClient.Credentials = null;
            CurrentUserPrincipal = null;

            CookieCollection cookies = DavClient.CookieContainer.GetAllCookies();
            foreach (Cookie cookie in cookies)
            {
                cookie.Expires = DateTime.UtcNow.AddDays(-30);
            }

            secureStorage.RemoveSensitiveData(CredentialsStorageKey);

            isAuthenticateSucceeded = false;
            LoginStatusChanged(this, EngineAuthentificationStatus.LoggedOut);
            await StopAsync();
        }

        /// <summary>
        /// Sets remote storage item ID for the root folder and initializes other Engine values if needed.
        /// </summary>
        /// <returns></returns>
        private async Task InitAsync(CancellationToken cancellationToken)
        {
            // Set the remote storage item ID for the root folder. It will be passed to the IEngine.GetFileSystemItemAsync()
            // method as a remoteStorageItemId parameter when a root folder is requested.
            IMetadata rootMetadata = await GetRootRemoteStorageMetadata(RemoteStorageRootPath, cancellationToken);
            SetRemoteStorageRootItemId(rootMetadata.RemoteStorageItemId);

            // Update root node display name and icon in Windows Explorer.
            //var storageFolder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(Path);
            //var storageInfo = Windows.Storage.Provider.StorageProviderSyncRootManager.GetSyncRootInformationForFolder(storageFolder);
            //storageInfo.DisplayNameResource = rootMetadata.Name;
            ////storageInfo.IconResource =  
            //StorageProviderSyncRootManager.Register(storageInfo);

        }

        static internal IncomingSyncMode GetSyncMode(IncomingSyncModeSetting preferedSyncMode)
        {
            switch (preferedSyncMode)
            {
                case IncomingSyncModeSetting.Off:
                    return IncomingSyncMode.Disabled;
                case IncomingSyncModeSetting.SyncId:
                    return IncomingSyncMode.SyncId;
                case IncomingSyncModeSetting.CRUD:
                    return IncomingSyncMode.Disabled;
                case IncomingSyncModeSetting.TimerPooling:
                    return IncomingSyncMode.TimerPooling;
                case IncomingSyncModeSetting.Auto:
                    return IncomingSyncMode.SyncId;
                default:
                    return IncomingSyncMode.SyncId;
            }
        }

        /// <summary>
        /// Creates and starts remote storage monitor, depending on sync mode.
        /// </summary>
        /// <param name="preferedSyncMode">Prefered sync mode.</param>
        /// <returns>Remote storage monitor or null if sync mode is not supported or sockets failed to connect.</returns>
        private async Task<RemoteStorageMonitorBase> StartRemoteStorageMonitorAsync(IncomingSyncModeSetting preferedSyncMode, CancellationToken cancellationToken)
        {
            RemoteStorageMonitorBase monitor = null;
            try
            {
                switch (preferedSyncMode)
                {
                    case IncomingSyncModeSetting.Off:
                        break;
                    case IncomingSyncModeSetting.SyncId:
                        monitor = new RemoteStorageMonitorSyncId(webSocketServerUrl, RemoteStorageRootPath, this);
                        break;
                    case IncomingSyncModeSetting.CRUD:
                        monitor = new RemoteStorageMonitorCRUD(webSocketServerUrl, RemoteStorageRootPath, this);
                        break;
                    case IncomingSyncModeSetting.TimerPooling:
                        break;
                    case IncomingSyncModeSetting.Auto:
                        if (SyncService.IsSyncIdSupported)
                        {
                            monitor = new RemoteStorageMonitorSyncId(webSocketServerUrl, RemoteStorageRootPath, this);
                        }
                        else
                        {
                            monitor = new RemoteStorageMonitorCRUD(webSocketServerUrl, RemoteStorageRootPath, this);
                        }
                        break;
                }

                if (monitor != null)
                {

                    monitor.Credentials = DavClient.Credentials != null && DavClient.Credentials is NetworkCredential ?
                                          (DavClient.Credentials as NetworkCredential) : null;
                    monitor.Cookies = DavClient.CookieContainer.GetAllCookies();
                    monitor.InstanceId = InstanceId;
                    monitor.ServerNotifications = ServerNotifications(Path, monitor.Logger);
                    await monitor.StartAsync();
                }

            }
            catch (Exception ex)
            {
                Logger.LogMessage($"Failed to create remote storage monitor. {ex.Message}", Path);
                monitor = null;
                SyncService.IncomingSyncMode = IncomingSyncMode.Disabled;
            }
            Commands.RemoteStorageMonitor = monitor;

            return monitor;
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();
            if (RemoteStorageMonitor != null)
            {
                await RemoteStorageMonitor?.StopAsync();
                RemoteStorageMonitor?.Dispose();
                RemoteStorageMonitor = null;
            }
        }


        /// <summary>
        /// Executes the specified command on a list of items provided in the protocol parameters.
        /// </summary>
        /// <param name="protocolParameters">
        /// An instance of <see cref="ProtocolParameters"/> containing the list of item URLs, 
        /// the command to execute, and other relevant data.
        /// </param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method performs different actions based on the command specified in the protocol parameters:
        /// - If the command is "lock", it locks the specified items.
        /// - If the command is "unlock", it unlocks the specified items.
        /// - For other commands ("openwith", "print", or "edit"), it executes the appropriate verb action on the items.
        /// 
        /// Each item URL is processed by:
        /// 1. Converting the item URL to a local file system path based on the remote storage root path.
        /// 2. Decoding and normalizing the item path.
        /// 3. Executing the corresponding action based on the command.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="protocolParameters"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if any required property (e.g., Command or ItemUrls) is missing or invalid.
        /// </exception>
        public async Task ExecuteCommandAsync(ProtocolParameters protocolParameters)
        {
            foreach (string itemUrl in protocolParameters.ItemUrls)
            {
                string itemPath = Mapping.ReverseMapPath(itemUrl);

                // Update item if synchronization is disabled.
                if (this.SyncService.IncomingSyncMode == IncomingSyncMode.Disabled)
                {
                    IWebDavResponse<ITHit.WebDAV.Client.IFile> resp = await this.DavClient.GetFileAsync(new Uri(itemUrl), Mapping.GetDavProperties());
                    IMetadata metadata = Mapping.GetMetadata(resp.WebDavResponse);
                    await this.ServerNotifications(itemPath).UpdateAsync(metadata);
                }

                switch (protocolParameters.Command)
                {
                    case CommandType.Lock:
                        await ClientNotifications(itemPath).LockAsync();
                        break;
                    case CommandType.Unlock:
                        await ClientNotifications(itemPath).UnlockAsync();
                        break;
                    case CommandType.OpenWith:
                        ClientNotifications(itemPath).ExecVerb(Verb.OpenWith);
                        break;
                    case CommandType.Print:
                        ClientNotifications(itemPath).ExecVerb(Verb.Print);
                        break;
                    case CommandType.Edit:
                        ClientNotifications(itemPath).ExecVerb(Verb.Edit);
                        break;
                    case CommandType.Open:
                        ClientNotifications(itemPath).ExecVerb(Verb.Open);
                        break;
                    default:
                        ClientNotifications(itemPath).ExecVerb(Verb.Default);
                        break;
                }
            }
        }

        public override async Task<OperationResult> OnItemsChangingAsync(ItemsChangeEventArgs e)
        {
            //if ((e.ComponentName != "Outgoing Sync")  && (e.Direction == SyncDirection.Outgoing) )
            //{
            //    var changedItem = e.Items?.FirstOrDefault();
            //    Logger.LogMessage($"{e.Direction}:{e.Source}:{e.OperationType} canceled", changedItem?.Path, changedItem?.NewPath, e.OperationContext, changedItem?.Metadata);
            //    return new OperationResult(OperationStatus.Failed, 0, "Only manual Outgoing Sync supported");
            //}

            return await base.OnItemsChangingAsync(e);
        }

        /// <summary>
        /// Gets remote storage metadata for the root folder.
        /// </summary>
        private async Task<IMetadata> GetRootRemoteStorageMetadata(string webDAVServerUrl, CancellationToken cancellationToken)
        {
            // Sending request to the server.
            IWebDavResponse<IHierarchyItem> response = await DavClient.GetItemAsync(new Uri(webDAVServerUrl), Mapping.GetDavProperties(), null, cancellationToken);
            IHierarchyItem rootFolder = response.WebDavResponse;

            IMetadata metadata = Mapping.GetMetadata(rootFolder);

            // If remote storage ID is not returned, the SyncID collection synchronization is not supported.
            if (metadata.RemoteStorageItemId == null)
            {
                Logger.LogMessage("Root resource-id is null.", Path);
            }

            return metadata;
        }

        /// <summary>
        /// Creates and configures WebDAV client to access the remote storage.
        /// </summary>
        /// <param name="engineInstanceId">Engine instance ID to be sent with every request to the remote storage.</param>
        private WebDavSession CreateWebDavSession(Guid engineInstanceId)
        {
            System.Net.Http.HttpClientHandler handler = new System.Net.Http.HttpClientHandler()
            {
                AllowAutoRedirect = false,

                // To enable pre-authentication (to avoid double requests) uncomment the code below.
                // This option improves performance but is less secure. 
                // PreAuthenticate = true,
            };
            WebDavSession davClient = new WebDavSession(Settings.WebDAVClientLicense);
            davClient.Client.Timeout = TimeSpan.FromMinutes(10);

            davClient.WebDavError += DavClient_WebDavError;
            davClient.WebDavMessage += DavClient_WebDAVMessage;
            davClient.CustomHeaders.Add("InstanceId", engineInstanceId.ToString());
            // Is required for IIS WebDAV server to avoid 417 Expectation Failed response.
            davClient.Client.DefaultRequestHeaders.ExpectContinue = false;

            if (secureStorage.TryGetSensitiveData(CredentialsStorageKey, out BasicAuthCredentials credentials))
            {
                davClient.Credentials = new NetworkCredential(credentials.UserName, credentials.Password);
            }
            else if (secureStorage.TryGetSensitiveData(CredentialsStorageKey, out CookieCollection cookies))
            {
                davClient.CookieContainer.Add(cookies);
            }

            return davClient;
        }

        /// <summary>
        /// Fired on every request to the WebDAV server. 
        /// </summary>
        /// <param name="sender">Request to the WebDAV client.</param>
        /// <param name="e">WebDAV message details.</param>
        private void DavClient_WebDAVMessage(ISession client, WebDavMessageEventArgs e)
        {
            Logger.LogDebug(e.Message);
        }

        private void DavClient_WebDavError(ISession sender, WebDavErrorEventArgs e)
        {
            if (e.Exception is WebDavHttpException httpException)
            {
                switch (httpException.Status.Code)
                {
                    case 401:
                    case 403:
                    case 302:
                        isAuthenticateSucceeded = false;
                        break;
                }
            }
        }

        private bool ShowLoginDialog(WebDavHttpException httpException, string userFileSystemPath)
        {
            bool success = false;
            if (httpException != null)
            {
                Uri failedUri = httpException.Uri;

                switch (httpException.Status.Code)
                {
                    // 302 redirect to login page.
                    case 302:
                    // 403 access to the requested resource is forbidden
                    case 403:
                        // Show login dialog.

                        // Azure AD can not navigate directly to login page - failed corelation.
                        //string loginUrl = ((Redirect302Exception)e.Exception).Location;
                        //Uri url = new System.Uri(loginUrl, System.UriKind.Absolute);

                        Logger.LogDebug($"{httpException?.Status.Code} {httpException?.Status.Description} {httpException.Message}", null, failedUri?.OriginalString);

                        success = WebBrowserLogin(failedUri);

                        // Replay the request, so the listing or update can complete succesefully.
                        // Unless this is LOCK - incorrect lock owner map be passed in this case.
                        //bool isLock = httpException.HttpMethod.NotEquals("LOCK", StringComparison.InvariantCultureIgnoreCase);
                        //bool isLock = false;
                        //e.Result = isLock ? WebDavErrorEventResult.Fail : WebDavErrorEventResult.Repeat;

                        break;

                    // Challenge-responce auth: Basic, Digest, NTLM or Kerberos
                    case 401:

                        Logger.LogDebug($"{httpException?.Status.Code} {httpException?.Status.Description} {httpException.Message}", null, failedUri?.OriginalString);

                        if (loginRetriesCurrent < loginRetriesMax)
                        {
                            success = ChallengeLogin(failedUri);
                        }
                        break;
                    default:
                        ILogger logger = Logger.CreateLogger("WebDAV Session");
                        logger.LogMessage($"{httpException.Status.Code} {httpException.Message}", null, failedUri?.OriginalString);
                        break;
                }
            }

            return success;
        }

        private bool WebBrowserLogin(Uri failedUri)
        {
            bool success = false;
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            ServiceProvider.DispatcherQueue.TryEnqueue(() =>
            {
                WebBrowserLogin webBrowserLoginWindow = new WebBrowserLogin(failedUri,
                    (cookies) =>
                        {
                            // Set cookies collected from the web browser dialog.
                            DavClient.CookieContainer.Add(cookies);

                            // Save cookies to the secure storage.
                            secureStorage.SaveSensitiveData(CredentialsStorageKey, cookies);
                            success = true;
                            tcs.SetResult(true);
                        }, log, true);
                webBrowserLoginWindow.Closed += (sender, e) => { tcs.TrySetResult(true); };
                webBrowserLoginWindow.Show();

            });
            tcs.Task.Wait();

            // Request currenly loged-in user name or ID from server here and set it below. 
            // In case of WebDAV current-user-principal can be used for this purpose.
            // For demo purposes we just set "DemoUserX".
            CurrentUserPrincipal = "DemoUserX";
            return success;
        }

        private bool ChallengeLogin(Uri failedUri)
        {
            bool succeed = false;
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            ServiceProvider.DispatcherQueue.TryEnqueue(async () =>
            {
                CredentialPickerLogin challengeWindow = new CredentialPickerLogin(failedUri, log);
                challengeWindow.Show();

                while (!succeed)
                {
                    // Show the login dialog.   
                    (NetworkCredential Credential, bool CredentialSaveOption)? result = await challengeWindow.ShowCredentialPickerAsync();

                    if (result == null)
                    {
                        // User canceled the dialog.
                        break;
                    }

                    DavClient.Credentials = result.Value.Credential;
                    CurrentUserPrincipal = result.Value.Credential.UserName;

                    try
                    {   // Retry the request with new credentials.
                        await GetRootRemoteStorageMetadata(RemoteStorageRootPath, default);
                        succeed = true;
                    }
                    catch (WebDavHttpException)
                    {
                        // Failed to authenticate.    
                    }

                    if (succeed)
                    {
                        if (result.Value.CredentialSaveOption)
                        {
                            // Save credentials to the secure storage.           
                            secureStorage.SaveSensitiveData(CredentialsStorageKey, new BasicAuthCredentials
                            {
                                UserName = result.Value.Credential.UserName,
                                Password = result.Value.Credential.Password
                            });
                        }
                        else
                        {
                            // Remove credentials from the secure storage.      
                            secureStorage.RemoveSensitiveData(CredentialsStorageKey);
                        }
                    }
                }

                challengeWindow?.Close();

                tcs.SetResult(true);
            });
            tcs.Task.Wait();

            return succeed;
        }

        private bool disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    RemoteStorageMonitor?.Dispose();
                    DavClient?.Dispose();
                }

                disposedValue = true;
            }
            base.Dispose(disposing);
        }
    }
}
