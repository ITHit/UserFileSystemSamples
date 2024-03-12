using System;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.WebDAV.Client;
using ITHit.WebDAV.Client.Exceptions;
using Microsoft.VisualBasic.Logging;
using System.Security;
using WebDAVDrive.UI.ViewModels;
using NotImplementedException = System.NotImplementedException;
using WebDAVDrive.UI;
using ITHit.FileSystem.Synchronization;


namespace WebDAVDrive
{
    /// <inheritdoc />
    public class VirtualEngine : VirtualEngineBase
    {
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
        /// Credentials used to connect to the server. 
        /// Used for challenge-responce auth (Basic, Digest, NTLM or Kerberos).
        /// </summary>
        public NetworkCredential Credentials { get; set; }

        /// <summary>
        /// Cookies used to connect to the server. 
        /// Used for cookies auth and MS-OFBA auth.
        /// </summary>
        public CookieCollection Cookies { get; set; } = new CookieCollection();

        /// <summary>
        /// Tray UI.
        /// </summary>
        //public readonly WindowsTrayInterface Tray;

        /// <summary>
        /// Commands.
        /// </summary>
        public readonly Commands Commands;

        /// <summary>
        /// Automatic lock timout in milliseconds.
        /// </summary>
        private readonly double autoLockTimoutMs;

        /// <summary>
        /// Manual lock timout in milliseconds.
        /// </summary>
        private readonly double manualLockTimoutMs;

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
        private string Title
        {
            get { return $"{productName} - {RemoteStorageRootPath}"; }
        }

        /// <summary>
        /// Storage key under which credentials are stored.
        /// </summary>
        private string CredentialsStorageKey
        {
            get { return $"{productName} - {RemoteStorageRootPath}"; }
        }

        /// <summary>
        /// Creates a vitual file system Engine.
        /// </summary>
        /// <param name="license">A license string.</param>
        /// <param name="userFileSystemRootPath">
        /// A root folder of your user file system. 
        /// Your file system tree will be located under this folder.
        /// </param>
        /// <param name="remoteStorageRootPath">Path to the remote storage root.</param>
        /// <param name="webSocketServerUrl">Web sockets server that sends notifications about changes on the server.</param>
        /// <param name="iconsFolderPath">Path to the icons folder.</param>
        /// <param name="autoLockTimoutMs">Automatic lock timout in milliseconds.</param>
        /// <param name="manualLockTimoutMs">Manual lock timout in milliseconds.</param>
        /// <param name="setLockReadOnly">Mark documents locked by other users as read-only for this user and vice versa.</param>
        /// <param name="logFormatter">Formats log output.</param>
        public VirtualEngine(
            string license,
            string userFileSystemRootPath,
            string remoteStorageRootPath,
            string webSocketServerUrl,
            string iconsFolderPath,
            double autoLockTimoutMs,
            double manualLockTimoutMs,
            bool setLockReadOnly,
            LogFormatter logFormatter,
            string productname)
            : base(license, userFileSystemRootPath, remoteStorageRootPath, iconsFolderPath, setLockReadOnly, logFormatter)
        {
            this.productName = productname;
            this.RemoteStorageRootPath = remoteStorageRootPath;
            this.webSocketServerUrl = webSocketServerUrl;
            this.log = logFormatter.Log;

            Mapping = new Mapping(Path, remoteStorageRootPath);

            

            this.autoLockTimoutMs = autoLockTimoutMs;
            this.manualLockTimoutMs = manualLockTimoutMs;

            DavClient = CreateWebDavSession(InstanceId);

            Commands = new Commands(this, remoteStorageRootPath, logFormatter.Log);

            //// Create tray app.
            TrayUI.CreateTray(productName, remoteStorageRootPath, iconsFolderPath, Commands, this, this.InstanceId);
            //Tray = new WindowsTrayInterface(productName, remoteStorageRootPath, iconsFolderPath, Commands);
            
            //// Listen to engine notifications to change menu and icon states.
            //this.StateChanged += Tray.Engine_StateChanged;
            //this.SyncService.StateChanged += Tray.SyncService_StateChanged;
        }

        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(byte[] remoteStorageId, FileSystemItemType itemType, IContext context, ILogger logger = null)
        {
            string userFileSystemPath = context.FileNameHint;
            if (itemType == FileSystemItemType.File)
            {
                return new VirtualFile(remoteStorageId, userFileSystemPath, this, autoLockTimoutMs, manualLockTimoutMs, logger);
            }
            else
            {
                return new VirtualFolder(remoteStorageId, userFileSystemPath, this, autoLockTimoutMs, manualLockTimoutMs, logger);
            }
        }

        /// <inheritdoc/>
        public override async Task<IMenuCommand> GetMenuCommandAsync(Guid menuGuid, IOperationContext operationContext = null)
        {
            // For this method to be called you need to register a menu command handler.
            // See method description for more details.

            Logger.LogDebug($"{nameof(IEngine)}.{nameof(GetMenuCommandAsync)}()", menuGuid.ToString());

            if (menuGuid == typeof(ShellExtension.ContextMenuVerbIntegratedLock).GUID)
            {
                return new MenuCommandLock(this, this.Logger);
            }
            if (menuGuid == typeof(ShellExtension.ContextMenuVerbIntegratedCompare).GUID)
            {
                return new MenuCommandCompare(this, this.Logger);
            }
            if (menuGuid == typeof(ShellExtension.ContextMenuVerbIntegratedUnmount).GUID)
            {
                return new MenuCommandUnmount(this, this.Logger);
            }

            Logger.LogError($"Menu not found", Path, menuGuid.ToString());
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override async Task StartAsync(bool processModified = true, CancellationToken cancellationToken = default)
        {
            // Set the remote storage item ID for the root folder. It will be passed to the IEngine.GetFileSystemItemAsync()
            // method as a remoteStorageItemId parameter when a root folder is requested.
            byte[] remoteStorageItemId = await GetRootRemoteStorageItemId(this.RemoteStorageRootPath, cancellationToken);
            this.SetRemoteStorageRootItemId(remoteStorageItemId);

            await base.StartAsync(processModified, cancellationToken);

            // Start remote storage monitor.
            if (SyncService.IncomingSyncMode != IncomingSyncMode.TimerPooling)
            {
                if (RemoteStorageMonitor == null)
                {
                    Logger.LogMessage($"Prefered sync mode: {SyncService.IncomingSyncMode}", Path);

                    // Create and start monitor, depending on server capabilities and prefered SyncMode.
                    RemoteStorageMonitor = await TryCreateRemoteStorageMonitorAsync(SyncService.IncomingSyncMode);
                    
                    // If Sync ID & Manual pooling modes are not available, set timer pooling mode.
                    SyncService.IncomingSyncMode = RemoteStorageMonitor?.SyncMode ?? IncomingSyncMode.TimerPooling;
                    Logger.LogMessage($"Actual sync mode: {SyncService.IncomingSyncMode}", Path);

                    Commands.RemoteStorageMonitor = RemoteStorageMonitor;
                }
                else
                {
                    await RemoteStorageMonitor?.StartAsync();
                }
            }            
        }

        /// <summary>
        /// Creates and starts remote storage monitor, depending on sync mode.
        /// </summary>
        /// <param name="preferedSyncMode">Prefered sync mode.</param>
        /// <returns>Remote storage monitor or null if sync mode is not supported or sockets failed to connect.</returns>
        private async Task<RemoteStorageMonitorBase> TryCreateRemoteStorageMonitorAsync(IncomingSyncMode preferedSyncMode)
        {
            RemoteStorageMonitorBase monitor = null;
            try
            {
                if (preferedSyncMode == IncomingSyncMode.SyncId && SyncService.IsSyncIdSupported)
                {
                    monitor = new RemoteStorageMonitorSyncId(webSocketServerUrl, RemoteStorageRootPath, this);
                }
                else if (preferedSyncMode != IncomingSyncMode.TimerPooling)
                {
                    monitor = new RemoteStorageMonitorCRUDE(webSocketServerUrl, RemoteStorageRootPath, this);
                }
                if (monitor != null)
                {
                    monitor.Credentials = this.Credentials;
                    monitor.Cookies = this.Cookies;
                    monitor.InstanceId = this.InstanceId;
                    monitor.ServerNotifications = this.ServerNotifications(this.Path, monitor.Logger);
                    await monitor.StartAsync();
                }
            }
            catch (Exception ex)
            {
                monitor = null;
                // Sync ID & manual pooling modes are not available. Use timer pooling.
                Logger.LogMessage($"Failed to create remote storage monitor. {ex.Message}", Path);
            }
            
            return monitor;
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();
            await RemoteStorageMonitor?.StopAsync();
        }

        /// <summary>
        /// Gets remote storage item ID for the foor folder.
        /// </summary>
        private async Task<byte[]> GetRootRemoteStorageItemId(string webDAVServerUrl, CancellationToken cancellationToken)
        {
            // Sending request to the server.
            var response = await DavClient.GetItemAsync(new Uri(webDAVServerUrl), Mapping.GetDavProperties(), null, cancellationToken);
            IHierarchyItem rootFolder = response.WebDavResponse;

            byte[] remoteStorageItemId = Mapping.GetUserFileSystemItemMetadata(rootFolder).RemoteStorageItemId;

            // This sample requires synchronization support, verifying that the ID was returned.
            if (remoteStorageItemId == null)
            {
                throw new WebDavException("remote-id or parent-resource-id is not found. Your WebDAV server does not support collection synchronization. Upgrade your .NET WebDAV server to v13.2 or Java WebDAV server to v6.2 or later version.");
            }

            return remoteStorageItemId;
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
            WebDavSession davClient = new WebDavSession(Program.Settings.WebDAVClientLicense);
            davClient.WebDavError += DavClient_WebDavError;
            davClient.WebDavMessage += DavClient_WebDAVMessage;
            davClient.CustomHeaders.Add("InstanceId", engineInstanceId.ToString());
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

        /// <summary>
        /// Event handler to process WebDAV errors. 
        /// If server returns 401 or 302 response here we show the login dialog.
        /// </summary>
        /// <param name="sender">WebDAV session.</param>
        /// <param name="e">WebDAV error details.</param>
        private void DavClient_WebDavError(ISession sender, WebDavErrorEventArgs e)
        {
            WebDavHttpException httpException = e.Exception as WebDavHttpException;
            if (httpException != null)
            {
                Uri failedUri = httpException.Uri;

                switch (httpException.Status.Code)
                {
                    // 302 redirect to login page.
                    case 302:
                        // Show login dialog.

                        // Azure AD can not navigate directly to login page - failed corelation.
                        //string loginUrl = ((Redirect302Exception)e.Exception).Location;
                        //Uri url = new System.Uri(loginUrl, System.UriKind.Absolute);
                        
                        Logger.LogDebug($"{httpException?.Status.Code} {httpException?.Status.Description} {e.Exception.Message}", null, failedUri?.OriginalString);

                        WebBrowserLogin(failedUri);

                        // Replay the request, so the listing or update can complete succesefully.
                        // Unless this is LOCK - incorrect lock owner map be passed in this case.
                        //bool isLock = httpException.HttpMethod.NotEquals("LOCK", StringComparison.InvariantCultureIgnoreCase);
                        bool isLock = false;
                        e.Result = isLock ? WebDavErrorEventResult.Fail : WebDavErrorEventResult.Repeat;

                        break;

                    // Challenge-responce auth: Basic, Digest, NTLM or Kerberos
                    case 401:

                        Logger.LogDebug($"{httpException?.Status.Code} {httpException?.Status.Description} {e.Exception.Message}", null, failedUri?.OriginalString);

                        if (loginRetriesCurrent < loginRetriesMax)
                        {
                            e.Result = ChallengeLoginLogin(failedUri);
                        }
                        break;
                    default:
                        ILogger logger = this.Logger.CreateLogger("WebDAV Session");
                        logger.LogMessage($"{httpException.Status.Code} {e.Exception.Message}", null, failedUri?.OriginalString);
                        break;
                }
            }
        }

        private void WebBrowserLogin(Uri failedUri)
        {
            WebDAVDrive.UI.WebBrowserLogin webBrowserLogin = null;
            Thread thread = new Thread(() =>
            {
                webBrowserLogin = new WebDAVDrive.UI.WebBrowserLogin(failedUri, log);
                webBrowserLogin.Title = Title;
                webBrowserLogin.ShowDialog();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Request currenly loged-in user name or ID from server here and set it below. 
            // In case of WebDAV current-user-principal can be used for this purpose.
            // For demo purposes we just set "DemoUserX".
            this.CurrentUserPrincipal = "DemoUserX";

            // Set cookies collected from the web browser dialog.
            DavClient.CookieContainer.Add(webBrowserLogin.Cookies);
            this.Cookies = webBrowserLogin.Cookies;
        }

        private WebDavErrorEventResult ChallengeLoginLogin(Uri failedUri)
        {
            Windows.Security.Credentials.PasswordCredential passwordCredential = CredentialManager.GetCredentials(CredentialsStorageKey, log);
            if (passwordCredential != null)
            {
                passwordCredential.RetrievePassword();
                NetworkCredential networkCredential = new NetworkCredential(passwordCredential.UserName, passwordCredential.Password);
                DavClient.Credentials = networkCredential;
                this.Credentials = networkCredential;
                this.CurrentUserPrincipal = networkCredential.UserName;
                return WebDavErrorEventResult.Repeat;
            }
            else
            {
                string login = null;
                SecureString password = null;
                bool dialogResult = false;
                bool keepLogedin = false;

                // Show login dialog
                WebDAVDrive.UI.ChallengeLogin loginForm = null;
                Thread thread = new Thread(() =>
                {
                    loginForm = new WebDAVDrive.UI.ChallengeLogin();
                    ((ChallengeLoginViewModel)loginForm.DataContext).Url = failedUri.OriginalString;
                    ((ChallengeLoginViewModel)loginForm.DataContext).WindowTitle = Title;
                    loginForm.ShowDialog();

                    login = ((ChallengeLoginViewModel)loginForm.DataContext).Login;
                    password = ((ChallengeLoginViewModel)loginForm.DataContext).Password;
                    keepLogedin = ((ChallengeLoginViewModel)loginForm.DataContext).KeepLogedIn;
                    dialogResult = (bool)loginForm.DialogResult;
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();

                loginRetriesCurrent++;
                if (dialogResult)
                {
                    if (keepLogedin)
                    {
                        CredentialManager.SaveCredentials(CredentialsStorageKey, login, password);
                    }
                    NetworkCredential newNetworkCredential = new NetworkCredential(login, password);
                    DavClient.Credentials = newNetworkCredential;
                    this.Credentials = newNetworkCredential;
                    this.CurrentUserPrincipal = newNetworkCredential.UserName;
                    return WebDavErrorEventResult.Repeat;
                }
            }

            return WebDavErrorEventResult.Fail;
        }

        private bool disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    TrayUI.RemoveTray(this.InstanceId);
                    //Tray?.Dispose();
                    RemoteStorageMonitor?.Dispose();
                    DavClient?.Dispose();
                }

                disposedValue = true;
            }
            base.Dispose(disposing);
        }
    }
}
