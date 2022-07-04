using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

using log4net;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows.Package;

using ITHit.WebDAV.Client;
using ITHit.WebDAV.Client.Exceptions;
using System.Net.Http;

using WebDAVDrive.UI;
using WebDAVDrive.UI.ViewModels;
using ITHit.FileSystem.Windows.ShellExtension.ComInfrastructure;
using System.Linq;

namespace WebDAVDrive
{
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
        /// WebDAV client for accessing the WebDAV server.
        /// </summary>
        internal static WebDavSession DavClient;

        /// <summary>
        /// Event to be fired when the tray app exits or an exit key in the console is selected.
        /// </summary>
        private static readonly EventWaitHandle exitEvent = new EventWaitHandle(false, EventResetMode.ManualReset);

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

            switch (args.FirstOrDefault())
            {
#if DEBUG
                case "-InstallDevCert":
                    /// Called by <see cref="CertificateRegistrar.TryInstallCertificate"/> in elevated mode.
                    EnsureDevelopmentCertificateInstalled();
                    return;

                case "-UninstallDevCert":
                    /// Called by <see cref="CertificateRegistrar.TryUninstallCertificate"/> in elevated mode.
                    EnsureDevelopmentCertificateUninstalled();
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

                if (await RegisterSparsePackageAsync())
                {
                    using (LocalServer server = new LocalServer())
                    {
                        // In case of sparse package our app also processes COM calls, register to process them.
                        ShellExtensionRegistrar.RegisterHandlerClasses(server);

                        await RunEngine();
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"\n\n Press Shift-Esc to fully uninstall the app. Then start the app again.\n\n", ex);
                await ProcessUserInputAsync();
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

        /// <summary>
        /// Ensures the app is a packaged app or sparse package is registered.
        /// Registers sparse package if needed.
        /// </summary>
        /// <returns>
        /// True if the app is running with app identity or package identity or sparse package is registered. False - otherwise.
        /// </returns>
        private static async Task<bool> RegisterSparsePackageAsync()
        {
            if (PackageRegistrar.IsRunningWithIdentity())
            {
                return true; // App has identity, ready to run.
            }

            if (!PackageRegistrar.SparsePackageRegistered())
            {
#if DEBUG
                /// Registering sparse package requires a valid certificate.
                /// In the development mode we use the below call to install the development certificate.
                if (!EnsureDevelopmentCertificateInstalled())
                {
                    return false;
                }
#endif
                // In the case of a regular installer (msi) call this method during installation.
                // This method call should be omitted for packaged application.
                log.Info("\n\nRegistering sparse package...");
                await PackageRegistrar.RegisterSparsePackageAsync();
                log.Info("\nSparse package successfully registered. Restart the application.\n\n");

                return false;
            }

            return true;
        }

        private static async Task RunEngine()
        {
            // Register sync root and create app folders.
            await RegisterSyncRootAsync();

            using (DavClient = ConfigureWebDavSession())
            {
                using (Engine = new VirtualEngine(
                    Settings.UserFileSystemLicense,
                    Settings.UserFileSystemRootPath,
                    Settings.WebDAVServerUrl,
                    Settings.WebSocketServerUrl,
                    Settings.IconsFolderPath,
                    logFormatter))
                {
                    Engine.SyncService.SyncIntervalMs = Settings.SyncIntervalMs;
                    Engine.AutoLock = Settings.AutoLock;

                    // Start tray application in a separate thread.
                    WindowsTrayInterface.CreateTrayInterface(Settings.ProductName, Engine, exitEvent);

                    // Print Engine config, settings, console commands, logging headers.
                    await logFormatter.PrintEngineStartInfoAsync(Engine);

                    // Start processing OS file system calls.
                    await Engine.StartAsync();

#if DEBUG
                    // Opens Windows File Manager with user file system folder and remote storage folder.
                    ShowTestEnvironment();
#endif
                    // Read console input in a separate thread.
                    await ConsoleReadKeyAsync();

                    // Keep this application running and reading user input
                    // untill the tray app exits or an exit key in the console is selected. 
                    exitEvent.WaitOne();
                }
            }
        }

        private static async Task ConsoleReadKeyAsync()
        {
            Thread readKeyThread = new Thread(async () =>
            {
                await ProcessUserInputAsync();
                exitEvent.Set();
            });
            readKeyThread.IsBackground = true;
            readKeyThread.Start();
        }

        /// <summary>
        /// Creates and configures WebDAV client to access the remote storage.
        /// </summary>
        private static WebDavSession ConfigureWebDavSession()
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
            return davClient;
        }

        /// <summary>
        /// Event handler to process WebDAV messages. 
        /// </summary>
        /// <param name="sender">Request to the WebDAV client.</param>
        /// <param name="e">WebDAV message details.</param>
        private static void DavClient_WebDAVMessage(ISession client, WebDavMessageEventArgs e)
        {
            string msg = $"\n{e.Message}";

            if(logFormatter.DebugLoggingEnabled)
            {
                log.Debug($"{msg}\n");
            }

            /*
            if (e.LogLevel == ITHit.WebDAV.Client.Logger.LogLevel.Debug)
                log.Debug($"{msg}\n");
            else
                log.Info($"{msg}\n");
            */
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
            log.Info($"\n{httpException?.Status.Code} {httpException?.Status.Description} {e.Exception.Message} ");
            if (httpException != null)
            {
                switch (httpException.Status.Code)
                {
                    // 302 redirect to login page.
                    case 302:

                        // Show login dialog.

                        // Azure AD can not navigate directly to login page - failed corelation.
                        //string loginUrl = ((Redirect302Exception)e.Exception).Location;
                        //Uri url = new System.Uri(loginUrl, System.UriKind.Absolute);

                        Uri failedUri = (e.Exception as WebDavHttpException).Uri;

                        WebDAVDrive.UI.WebBrowserLogin webBrowserLogin = null;
                        Thread thread = new Thread(() => {
                            webBrowserLogin = new WebDAVDrive.UI.WebBrowserLogin(failedUri, DavClient, log);
                            webBrowserLogin.Title = Settings.ProductName;
                            webBrowserLogin.ShowDialog();
                        });
                        thread.SetApartmentState(ApartmentState.STA);
                        thread.Start();
                        thread.Join();
                        e.Result = WebDavErrorEventResult.Repeat;
                        break;

                    // Challenge-responce auth: Basic, Digest, NTLM or Kerberos
                    case 401:
                        if (loginRetriesCurrent < loginRetriesMax)
                        {
                            failedUri = (e.Exception as WebDavHttpException).Uri;
                            Windows.Security.Credentials.PasswordCredential passwordCredential = CredentialManager.GetCredentials(Settings.ProductName, log);
                            if (passwordCredential != null)
                            {
                                passwordCredential.RetrievePassword();
                                NetworkCredential networkCredential = new NetworkCredential(passwordCredential.UserName, passwordCredential.Password);
                                DavClient.Credentials = networkCredential;
                                Engine.Credentials = networkCredential;
                                e.Result = WebDavErrorEventResult.Repeat;
                            }
                            else
                            {
                                string login = null;
                                SecureString password = null;
                                bool dialogResult = false;
                                bool keepLogedin = false;

                                // Show login dialog
                                WebDAVDrive.UI.ChallengeLogin loginForm = null;
                                thread = new Thread(() =>
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
                                    e.Result = WebDavErrorEventResult.Repeat;
                                }
                            }
                        }
                        break;
                }
            }
        }

        private static async Task ProcessUserInputAsync()
        {
            do
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                switch (keyInfo.Key)
                {
                    case ConsoleKey.F1:
                    case ConsoleKey.H:
                        // Print help info.
                        logFormatter.PrintHelp();
                        break;

                    case ConsoleKey.E:
                        // Start/stop the Engine and all sync services.
                        if (Engine.State == EngineState.Running)
                        {
                            await Engine.StopAsync();
                        }
                        else if (Engine.State == EngineState.Stopped)
                        {
                            await Engine.StartAsync();
                        }
                        break;

                    case ConsoleKey.S:
                        // Start/stop full synchronization.
                        if (Engine.SyncService.SyncState == SynchronizationState.Disabled)
                        {
                            if (Engine.State != EngineState.Running)
                            {
                                Engine.SyncService.Logger.LogError("Failed to start. The Engine must be running.");
                                break;
                            }
                            await Engine.SyncService.StartAsync();
                        }
                        else
                        {
                            await Engine.SyncService.StopAsync();
                        }
                        break;

                    case ConsoleKey.D:
                        // Enables/disables debug logging.
                        logFormatter.DebugLoggingEnabled = !logFormatter.DebugLoggingEnabled;
                        break;

                    case ConsoleKey.M:
                        // Start/stop remote storage monitor.
                        if (Engine.RemoteStorageMonitor.SyncState == SynchronizationState.Disabled)
                        {
                            if (Engine.State != EngineState.Running)
                            {
                                Engine.RemoteStorageMonitor.Logger.LogError("Failed to start. The Engine must be running.");
                                break;
                            }
                            await Engine.RemoteStorageMonitor.StartAsync();
                        }
                        else
                        {
                            await Engine.RemoteStorageMonitor.StopAsync();
                        }
                        break;

                    case ConsoleKey.L:
                        // Open log file.
                        ProcessStartInfo psiLog = new ProcessStartInfo(logFormatter.LogFilePath);
                        psiLog.UseShellExecute = true;
                        using (Process.Start(psiLog))
                        {
                        }
                        break;

                    case ConsoleKey.B:
                        // Submit support tickets, report bugs, suggest features.
                        ProcessStartInfo psiSupport = new ProcessStartInfo("https://www.userfilesystem.com/support/");
                        psiSupport.UseShellExecute = true;
                        using (Process.Start(psiSupport))
                        {
                        }
                        break;

                    case ConsoleKey.Escape:
                        if (Engine?.State == EngineState.Running)
                        {
                            await Engine.StopAsync();
                        }

                        // Call the code below during programm uninstall using classic msi.
                        if (await Registrar.IsRegisteredAsync(Settings.UserFileSystemRootPath))
                        {
                            await UnregisterSyncRootAsync();
                        }

                        ShellExtensionRegistrar.Unregister(log);

                        // Delete all files/folders.
                        await CleanupAppFoldersAsync();

                        if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift))
                        {
                            // Uninstall developer certificate.
                            EnsureDevelopmentCertificateUninstalled();

                            // Uninstall conflicting packages if any
                            await EnsureConflictingPackagesUninstalled();

                            // Unregister sparse package.
                            await UnregisterSparsePackageAsync();
                        }

                        return;

                    case ConsoleKey.Spacebar:
                        if (Engine?.State == EngineState.Running)
                        {
                            await Engine.StopAsync();
                        }
                        log.Info("\n\nAll downloaded file / folder placeholders remain in file system. Restart the application to continue managing files.\n");
                        return;

                    default:
                        break;
                }

            } while (true);
        }

        /// <summary>
        /// Registers sync root and creates application folders.
        /// </summary>
        /// <remarks>
        /// In the case of a packaged installer (msix) call this method during first program start.
        /// In the case of a regular installer (msi) call this method during installation.
        /// </remarks>
        private static async Task RegisterSyncRootAsync()
        {
            if (!await Registrar.IsRegisteredAsync(Settings.UserFileSystemRootPath))
            {
                log.Info($"\n\nRegistering sync root.");
                Directory.CreateDirectory(Settings.UserFileSystemRootPath);

                await Registrar.RegisterAsync(SyncRootId, Settings.UserFileSystemRootPath, Settings.ProductName,
                    Path.Combine(Settings.IconsFolderPath, "Drive.ico"));

                if (!PackageRegistrar.IsRunningWithIdentity())
                {
                    ShellExtensionRegistrar.Register(SyncRootId, log);
                }
            }
            else
            {
                log.Info($"\n{Settings.UserFileSystemRootPath} sync root already registered.");
            }
        }

        /// <summary>
        /// Unregisters sync root.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the case of a packaged installer (msix) you do not need to call this method. 
        /// The platform will automatically delete sync root registartion during program uninstall.
        /// </para>
        /// <para>
        /// In the case of a regular installer (msi) call this method during uninstall.
        /// </para>
        /// </remarks>
        private static async Task UnregisterSyncRootAsync()
        {
            log.Info($"\n\nUnregistering sync root.");
            await Registrar.UnregisterAsync(SyncRootId);
        }

        private static async Task CleanupAppFoldersAsync()
        {
            log.Info("\n\nDeleting all file and folder placeholders.");
            try
            {
                if (Directory.Exists(Settings.UserFileSystemRootPath))
                {
                    Directory.Delete(Settings.UserFileSystemRootPath, true);
                }
            }
            catch (Exception ex)
            {
                log.Error("Failed to delete placeholders.", ex);
            }

            try
            {
                if (Engine != null)
                {
                    await Engine.UninstallCleanupAsync();
                }
            }
            catch (Exception ex)
            {
                log.Error($"\n{ex}");
            }
        }

        /// <summary>
        /// Unregisters sparse package.
        /// </summary>
        private static async Task UnregisterSparsePackageAsync()
        {
            log.Info("\n\nUnregistering sparse package...");
            await PackageRegistrar.UnregisterSparsePackageAsync();
            log.Info("\nSparse package unregistered sucessfully.");
        }

#if DEBUG
        /// <summary>
        /// Installs the development certificate.
        /// </summary>
        /// <remarks>
        /// In a real-world application your application will be signed with a trusted
        /// certificate and you do not need to install it.
        /// Development certificate installation is needed for sparse package only,
        /// should be omitted for packaged application.
        /// </remarks>
        /// <returns>True if the the certificate is installed, false - if the installation failed.</returns>
        private static bool EnsureDevelopmentCertificateInstalled()
        {
            string sparsePackagePath = PackageRegistrar.GetSparsePackagePath();
            CertificateRegistrar certificateRegistrar = new CertificateRegistrar(sparsePackagePath);
            if (!certificateRegistrar.IsCertificateInstalled())
            {
                log.Info("\n\nInstalling developer certificate...");
                if (certificateRegistrar.TryInstallCertificate(true, out int errorCode))
                {
                    log.Info("\nDeveloper certificate successfully installed.");
                }
                else
                {
                    log.Error($"\nFailed to install the developer certificate. Error code: {errorCode}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Uninstalls the development certificate.
        /// </summary>
        /// <returns>True if the the certificate is uninstalled, false - if the uninstallation failed.</returns>
        private static bool EnsureDevelopmentCertificateUninstalled()
        {
            string sparsePackagePath = PackageRegistrar.GetSparsePackagePath();
            CertificateRegistrar certRegistrar = new CertificateRegistrar(sparsePackagePath);
            if (certRegistrar.IsCertificateInstalled())
            {
                log.Info("\n\nUninstalling developer certificate...");
                if (certRegistrar.TryUninstallCertificate(true, out int errorCode))
                {
                    log.Info("\nDeveloper certificate successfully uninstalled.");
                }
                else
                {
                    log.Error($"\nFailed to uninstall the developer certificate. Error code: {errorCode}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Uninstalls packages that registered classes when sparse package contains them to prevent conflicts.
        /// </summary>
        /// <returns></returns>
        private static async Task EnsureConflictingPackagesUninstalled()
        {
            if (PackageRegistrar.IsRunningWithSparsePackageIdentity() && PackageRegistrar.ConflictingPackagesRegistered())
            {
                log.Info("\nUninstalling conflicting packages...");
                await PackageRegistrar.UnregisterConflictingPackages();
            }
        }

        /// <summary>
        /// Opens Windows File Manager with both remote storage and user file system for testing.
        /// </summary>
        /// <remarks>This method is provided solely for the development and testing convenience.</remarks>
        private static void ShowTestEnvironment()
        {
            // Enable UTF8 for Console Window and set width.
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.SetWindowSize(Console.LargestWindowWidth, Console.LargestWindowHeight / 3);
            Console.SetBufferSize(Console.LargestWindowWidth * 2, Console.BufferHeight);

            // Open Windows File Manager with user file system.
            ProcessStartInfo ufsInfo = new ProcessStartInfo(Program.Settings.UserFileSystemRootPath);
            ufsInfo.UseShellExecute = true; // Open window only if not opened already.
            using (Process ufsWinFileManager = Process.Start(ufsInfo))
            {

            }

            // Open web browser with WebDAV content.
            ProcessStartInfo rsInfo = new ProcessStartInfo(Program.Settings.WebDAVServerUrl);
            rsInfo.UseShellExecute = true; // Open window only if not opened already.
            using (Process rsWinFileManager = Process.Start(rsInfo))
            {

            }
        }
#endif

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
