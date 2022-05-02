using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.Extensions.Configuration;

using log4net;
using log4net.Appender;
using log4net.Config;
using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;

using ITHit.WebDAV.Client;
using ITHit.WebDAV.Client.Exceptions;
using System.Net.Http;

using WebDAVDrive.UI;
using WebDAVDrive.UI.ViewModels;


namespace WebDAVDrive
{
    class Program
    {
        /// <summary>
        /// Application settings.
        /// </summary>
        internal static AppSettings Settings;

        /// <summary>
        /// Processes OS file system calls, 
        /// synchronizes user file system to remote storage. 
        /// </summary>
        internal static VirtualEngine Engine;

        /// <summary>
        /// WebDAV client for accessing the WebDAV server.
        /// </summary>
        internal static WebDavSession DavClient;

        /// <summary>
        /// Log4Net logger.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Log file path.
        /// </summary>
        private static string LogFilePath;

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
            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build();
            Settings = configuration.ReadSettings();

            // Configure log4net and set log file path.
            LogFilePath = ConfigureLogger();

            PrintHelp();

            // Register sync root and create app folders.
            await RegisterSyncRootAsync();

            // Log indexed state.
            StorageFolder userFileSystemRootFolder = await StorageFolder.GetFolderFromPathAsync(Settings.UserFileSystemRootPath);
            log.Info($"\nIndexed state: {(await userFileSystemRootFolder.GetIndexedStateAsync())}\n");

            Logger.PrintHeader(log);

            using (DavClient = ConfigureWebDavSession())
            {
                try
                {
                    Engine = new VirtualEngine(
                        Settings.UserFileSystemLicense,
                        Settings.UserFileSystemRootPath,
                        Settings.WebDAVServerUrl,
                        Settings.WebSocketServerUrl,
                        Settings.IconsFolderPath,
                        Settings.RpcCommunicationChannelName,
                        Settings.SyncIntervalMs,
                        Settings.MaxDegreeOfParallelism,
                        log);
                    Engine.AutoLock = Settings.AutoLock;

                    // Start tray application in a separate thread.
                    WindowsTrayInterface.CreateTrayInterface(Settings.ProductName, Engine, exitEvent);

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
                catch (Exception ex)
                {
                    log.Error(ex);
                    await ProcessUserInputAsync();
                }
                finally
                {
                    Engine.Dispose();
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

#if DEBUG
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
            //if (e.LogLevel == LogLevel.Debug)
            //    log.Debug($"{msg}\n");
            //else
                log.Info($"{msg}\n");
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

        /// <summary>
        /// Configures log4net logger.
        /// </summary>
        /// <returns>Log file path.</returns>
        private static string ConfigureLogger()
        {
            // Load Log4Net for net configuration.
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "log4net.config")));

            // Update log file path for msix package. 
            RollingFileAppender rollingFileAppender = logRepository.GetAppenders().Where(p => p.GetType() == typeof(RollingFileAppender)).FirstOrDefault() as RollingFileAppender;
            if (rollingFileAppender != null && rollingFileAppender.File.Contains("WindowsApps"))
            {
                rollingFileAppender.File = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Settings.AppID,
                                                        Path.GetFileName(rollingFileAppender.File));
            }
            return rollingFileAppender?.File;
        }

        private static void PrintHelp()
        {
            log.Info($"\n{"AppID:",-15} {Settings.AppID}");
            log.Info($"\n{"Engine version:",-15} {typeof(IEngine).Assembly.GetName().Version}");
            log.Info($"\n{"OS version:",-15} {RuntimeInformation.OSDescription}");
            log.Info($"\n{"Env version:",-15} {RuntimeInformation.FrameworkDescription} {IntPtr.Size * 8}bit.");
            log.Info("\n\nPress Esc to unregister file system, delete all files/folders and exit (simulate uninstall).");
            log.Info("\nPress Spacebar to exit without unregistering (simulate reboot).");
            log.Info("\nPress 'e' to start/stop the Engine and all sync services.");
            log.Info("\nPress 's' to start/stop full synchronization service.");
            log.Info("\nPress 'm' to start/stop remote storage monitor.");
            log.Info($"\nPress 'l' to open log file. ({LogFilePath})");
            log.Info($"\nPress 'b' to submit support tickets, report bugs, suggest features. (https://userfilesystem.com/support/)");
            log.Info("\n----------------------\n");
        }

        private static async Task ProcessUserInputAsync()
        {
            do
            {
                switch (Console.ReadKey(true).KeyChar)
                {
                    case (char)ConsoleKey.F1:
                    case 'h':
                        // Print help info.
                        PrintHelp();
                        break;

                    case 'e':
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

                    case 's':
                        // Start/stop full synchronization.
                        if (Engine.SyncService.SyncState == SynchronizationState.Disabled)
                        {
                            if (Engine.State != EngineState.Running)
                            {
                                Engine.SyncService.LogError("Failed to start. The Engine must be running.");
                                break;
                            }
                            await Engine.SyncService.StartAsync();
                        }
                        else
                        {
                            await Engine.SyncService.StopAsync();
                        }
                        break;

                    case 'm':
                        // Start/stop remote storage monitor.
                        if (Engine.RemoteStorageMonitor.SyncState == SynchronizationState.Disabled)
                        {
                            if (Engine.State != EngineState.Running)
                            {
                                Engine.RemoteStorageMonitor.LogError("Failed to start. The Engine must be running.");
                                break;
                            }
                            await Engine.RemoteStorageMonitor.StartAsync();
                        }
                        else
                        {
                            await Engine.RemoteStorageMonitor.StopAsync();
                        }
                        break;

                    case 'l':
                        // Open log file.
                        ProcessStartInfo psiLog = new ProcessStartInfo(LogFilePath);
                        psiLog.UseShellExecute = true;
                        using (Process.Start(psiLog))
                        {
                        }
                        break;

                    case 'b':
                        // Submit support tickets, report bugs, suggest features.
                        ProcessStartInfo psiSupport = new ProcessStartInfo("https://www.userfilesystem.com/support/");
                        psiSupport.UseShellExecute = true;
                        using (Process.Start(psiSupport))
                        {
                        }
                        break;

                    case 'q':
                        // Unregister during programm uninstall.
                        Engine.Dispose();
                        await UnregisterSyncRootAsync();
                        log.Info("\nAll empty file and folder placeholders are deleted. Hydrated placeholders are converted to regular files / folders.\n");
                        return;

                    case (char)ConsoleKey.Escape:
                    case 'Q':
                        Engine.Dispose();

                        // Call the code below during programm uninstall using classic msi.
                        await UnregisterSyncRootAsync();

                        // Delete all files/folders.
                        await CleanupAppFoldersAsync();
                        return;

                    case (char)ConsoleKey.Spacebar:
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
                log.Info($"\nRegistering {Settings.UserFileSystemRootPath} sync root.");
                Directory.CreateDirectory(Settings.UserFileSystemRootPath);

                await Registrar.RegisterAsync(SyncRootId, Settings.UserFileSystemRootPath, Settings.ProductName,
                    Path.Combine(Settings.IconsFolderPath, "Drive.ico"));

                log.Info("\nRegistering shell extensions...\n");
                ShellExtensionRegistrar.Register(SyncRootId);
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
            log.Info($"\n\nUnregistering {Settings.UserFileSystemRootPath} sync root.");
            await Registrar.UnregisterAsync(SyncRootId);

            log.Info("\nUnregistering shell extensions...\n");
            ShellExtensionRegistrar.Unregister();
        }

        private static async Task CleanupAppFoldersAsync()
        {
            log.Info("\nDeleting all file and folder placeholders.\n");
            try
            {
                Directory.Delete(Settings.UserFileSystemRootPath, true);
            }
            catch (Exception ex)
            {
                log.Error($"\n{ex}");
            }

            try
            {
                await ((EngineWindows)Engine).UninstallCleanupAsync();
            }
            catch (Exception ex)
            {
                log.Error($"\n{ex}");
            }
        }
    }
}
