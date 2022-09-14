using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Configuration;

using log4net;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows.Package;

using ITHit.WebDAV.Client;
using ITHit.WebDAV.Client.Exceptions;
using System.Net.Http;

using WebDAVDrive.UI;
using WebDAVDrive.UI.ViewModels;

namespace WebDAVDrive
{
    /// <summary>
    /// - To run this app with a sparse package application identity run this project directly from Virual Studio in debug mode.
    /// This will install the dev certificate and register sparse package. Then restart the project to use the app.
    /// Shell extensions are installed/ununstalled automatically via sparse package manifest located in this project.
    /// 
    /// - To run this app with a package identity run the .Package project provided with this sample.
    /// Packaged application enables deployment to Microsoft Store.
    /// Shell extensions are installed/ununstalled automatically via a manifest located in the .Package project.
    /// 
    /// - To run this app without identity comment out the <see cref="SparsePackageInstaller.RegisterSparsePackageAsync"/> call in
    /// this class and run this project.
    /// Shell extensions are installed/uninstalled using the <see cref="Registrar"/> class, when sync root is registered/unregistered.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Application settings.
        /// </summary>
        internal static AppSettings Settings;

        /// <summary>
        /// Log4Net logger.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Processes OS file system calls,
        /// synchronizes user file system to remote storage.
        /// </summary>
        internal static VirtualEngine Engine;

        /// <summary>
        /// Outputs logging information.
        /// </summary>
        private static LogFormatter logFormatter;

        /// <summary>
        /// Provides sync root registration functionality and sparse package installation.
        /// </summary>
        private static SparsePackageRegistrar registrar;

        /// <summary>
        /// Application commands.
        /// </summary>
        private static Commands commands;

        /// <summary>
        /// Processes console input.
        /// </summary>
        private static ConsoleProcessor consoleProcessor;

        /// <summary>
        /// WebDAV client for accessing the WebDAV server.
        /// </summary>
        internal static WebDavSession DavClient;

        /// <summary>
        /// Maximum number of login attempts.
        /// </summary>
        private static uint loginRetriesMax = 3;

        /// <summary>
        /// Current login attempt.
        /// </summary>
        private static uint loginRetriesCurrent = 0;


        static async Task Main(string[] args)
        {
            // Load Settings.
            Settings = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build().ReadSettings();

            logFormatter = new LogFormatter(log, Settings.AppID, Settings.WebDAVServerUrl);

            // Log environment description.
            logFormatter.PrintEnvironmentDescription();

            registrar = new SparsePackageRegistrar(SyncRootId, Settings.UserFileSystemRootPath, log, ShellExtension.ShellExtensions.Handlers);

            commands = new Commands(log, Settings.WebDAVServerUrl);
            consoleProcessor = new ConsoleProcessor(registrar, logFormatter, commands);

            switch (args.FirstOrDefault())
            {
#if DEBUG
                case "-InstallDevCert":
                    /// Called by <see cref="CertificateRegistrar.TryInstallCertificate"/> in elevated mode.
                    registrar.EnsureDevelopmentCertificateInstalled();
                    return;

                case "-UninstallDevCert":
                    /// Called by <see cref="CertificateRegistrar.TryUninstallCertificate"/> in elevated mode.
                    registrar.EnsureDevelopmentCertificateUninstalled();
                    return;
#endif
                case "-Embedding":
                    // Called by COM to access COM components.
                    // https://docs.microsoft.com/en-us/windows/win32/com/localserver32#remarks
                    return;
            }

            try
            {
                ValidatePackagePrerequisites();

                // Install dev cert and register sparse package.
                if (!await registrar.RegisterSparsePackageAsync())
                {
                    return; // Sparse package registered - restart the sample.
                }

                // Register this app to process COM shell extensions calls.
                using (ShellExtension.ShellExtensions.StartComServer(Settings.ShellExtensionsComServerRpcEnabled))
                {
                    // Run the User File System Engine.
                    await RunEngine();
                }
            }
            catch (Exception ex)
            {
                log.Error($"\n\n Press Shift-Esc to fully uninstall the app. Then start the app again.\n\n", ex);
                await consoleProcessor.ProcessUserInputAsync();
            }
        }

        /// <summary>
        /// Check if there is any remains from previous program deployments.
        /// </summary>
        private static void ValidatePackagePrerequisites()
        {
            if (PackageRegistrar.IsRunningWithIdentity())
            {
                PackageRegistrar.EnsureIdentityContextIsCorrect();
                PackageRegistrar.EnsureNoConflictingClassesRegistered();
            }
        }

        private static async Task RunEngine()
        {
            // Register sync root and create app folders.
            await registrar.RegisterSyncRootAsync(Settings.ProductName, Path.Combine(Settings.IconsFolderPath, "Drive.ico"), Settings.ShellExtensionsComServerExePath);

            using (Engine = new VirtualEngine(
                Settings.UserFileSystemLicense,
                Settings.UserFileSystemRootPath,
                Settings.WebDAVServerUrl,
                Settings.WebSocketServerUrl,
                Settings.IconsFolderPath,
                logFormatter))
            {
                commands.Engine = Engine;
                commands.RemoteStorageMonitor = Engine.RemoteStorageMonitor;

                Engine.SyncService.SyncIntervalMs = Settings.SyncIntervalMs;
                Engine.IncomingFullSync.SyncIntervalMs = Settings.IncomingFullSyncIntervalMs;
                Engine.AutoLock = Settings.AutoLock;
                Engine.MaxTransferConcurrentRequests = Settings.MaxTransferConcurrentRequests.Value;
                Engine.MaxOperationsConcurrentRequests = Settings.MaxOperationsConcurrentRequests.Value;
                Engine.ShellExtensionsComServerRpcEnabled = Settings.ShellExtensionsComServerRpcEnabled; // Enable RPC in case RPC shaell extension handlers, hosted in separate process. 

                // Print console commands.
                consoleProcessor.PrintHelp();

                // Print Engine config, settings, logging headers.
                await logFormatter.PrintEngineStartInfoAsync(Engine);

                using (DavClient = CreateWebDavSession(Engine.InstanceId))
                {
                    // Start processing OS file system calls.
                    await Engine.StartAsync();
#if DEBUG
                    // Opens Windows File Manager with user file system folder and remote storage folder.
                    commands.ShowTestEnvironment();
#endif
                    // Keep this application running and reading user input
                    // untill the tray app exits or an exit key in the console is selected.
                    Task console = StartConsoleReadKeyAsync();
                    Task tray = WindowsTrayInterface.StartTrayInterfaceAsync(Settings.ProductName, Settings.IconsFolderPath, commands, Engine);
                    Task.WaitAny(console, tray);
                }
            }
        }

        private static async Task StartConsoleReadKeyAsync()
        {
            await Task.Run(async () => await consoleProcessor.ProcessUserInputAsync());
        }

        /// <summary>
        /// Creates and configures WebDAV client to access the remote storage.
        /// </summary>
        /// <param name="engineInstanceId">Engine instance ID to be sent with every request to the remote storage.</param>
        private static WebDavSession CreateWebDavSession(Guid engineInstanceId)
        {
            HttpClientHandler handler = new HttpClientHandler()
            {
                AllowAutoRedirect = false,

                // Enable pre-authentication to avoid double requests.
                // This option improves performance but is less secure. 
                // PreAuthenticate = true,
            };
            WebDavSession davClient = new WebDavSession(Program.Settings.WebDAVClientLicense);
            davClient.WebDavError += DavClient_WebDavError;
            davClient.WebDavMessage += DavClient_WebDAVMessage;
            davClient.CustomHeaders.Add("ITHitUserFileSystemEngineInstanceId", engineInstanceId.ToString());
            return davClient;
        }

        /// <summary>
        /// Fired on every request to the WebDAV server. 
        /// </summary>
        /// <param name="sender">Request to the WebDAV client.</param>
        /// <param name="e">WebDAV message details.</param>
        private static void DavClient_WebDAVMessage(ISession client, WebDavMessageEventArgs e)
        {
            string msg = $"\n{e.Message}";

            if (logFormatter.DebugLoggingEnabled)
            {
                log.Debug($"{msg}\n");
            }
        }

        /// <summary>
        /// Event handler to process WebDAV errors. 
        /// If server returns 401 or 302 response here we show the login dialog.
        /// </summary>
        /// <param name="sender">WebDAV session.</param>
        /// <param name="e">WebDAV error details.</param>
        private static void DavClient_WebDavError(ISession sender, WebDavErrorEventArgs e)
        {
            WebDavHttpException httpException = e.Exception as WebDavHttpException;
            if (httpException != null)
            {
                switch (httpException.Status.Code)
                {
                    // 302 redirect to login page.
                    case 302:
                        log.Debug($"\n{httpException?.Status.Code} {httpException?.Status.Description} {e.Exception.Message} ");

                        // Show login dialog.

                        // Azure AD can not navigate directly to login page - failed corelation.
                        //string loginUrl = ((Redirect302Exception)e.Exception).Location;
                        //Uri url = new System.Uri(loginUrl, System.UriKind.Absolute);

                        Uri failedUri = (e.Exception as WebDavHttpException).Uri;

                        WebBrowserLogin(failedUri);

                        // Replay the request, so the listing can complete succesefully.
                        e.Result = WebDavErrorEventResult.Repeat;
                        break;

                    // Challenge-responce auth: Basic, Digest, NTLM or Kerberos
                    case 401:
                        log.Debug($"\n{httpException?.Status.Code} {httpException?.Status.Description} {e.Exception.Message} ");

                        if (loginRetriesCurrent < loginRetriesMax)
                        {
                            failedUri = (e.Exception as WebDavHttpException).Uri;
                            e.Result = ChallengeLoginLogin(failedUri);
                        }
                        break;
                    default:
                        ILogger logger = Engine.Logger.CreateLogger("WebDAV Session");
                        logger.LogMessage($"{httpException.Status.Code} {e.Exception.Message}", httpException.Uri.ToString());
                        break;
                }
            }
        }

        private static void WebBrowserLogin(Uri failedUri)
        {
            WebDAVDrive.UI.WebBrowserLogin webBrowserLogin = null;
            Thread thread = new Thread(() =>
            {
                webBrowserLogin = new WebDAVDrive.UI.WebBrowserLogin(failedUri, log);
                webBrowserLogin.Title = Settings.ProductName;
                webBrowserLogin.ShowDialog();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Set cookies collected from the web browser dialog.
            DavClient.CookieContainer.Add(webBrowserLogin.Cookies);
        }

        private static WebDavErrorEventResult ChallengeLoginLogin(Uri failedUri)
        {
            Windows.Security.Credentials.PasswordCredential passwordCredential = CredentialManager.GetCredentials(Settings.ProductName, log);
            if (passwordCredential != null)
            {
                passwordCredential.RetrievePassword();
                NetworkCredential networkCredential = new NetworkCredential(passwordCredential.UserName, passwordCredential.Password);
                DavClient.Credentials = networkCredential;
                Engine.Credentials = networkCredential;
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
                    ((ChallengeLoginViewModel)loginForm.DataContext).WindowTitle = Settings.ProductName;
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
                        CredentialManager.SaveCredentials(Settings.ProductName, login, password);
                    }
                    NetworkCredential newNetworkCredential = new NetworkCredential(login, password);
                    Engine.Credentials = newNetworkCredential;
                    DavClient.Credentials = newNetworkCredential;
                    return WebDavErrorEventResult.Repeat;
                }
            }

            return WebDavErrorEventResult.Fail;
        }

        /// <summary>
        /// Gets automatically generated Sync Root ID.
        /// </summary>
        /// <remarks>An identifier in the form: [Storage Provider ID]![Windows SID]![Account ID]</remarks>
        private static string SyncRootId
        {
            get
            {
                return $"{Settings.AppID}!{System.Security.Principal.WindowsIdentity.GetCurrent().User}!User";
            }
        }
    }
}
