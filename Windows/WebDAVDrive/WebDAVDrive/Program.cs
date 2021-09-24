using log4net;
using log4net.Appender;
using log4net.Config;
using Microsoft.Extensions.Configuration;
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
using WebDAVDrive.UI;
using WebDAVDrive.UI.ViewModels;
using Windows.Storage;

using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.WebDAV.Client;
using ITHit.WebDAV.Client.Exceptions;
using ITHit.WebDAV.Client.Logger;
using System.Net.Http;

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
        public static VirtualEngine Engine;

        /// <summary>
        /// WebDAV client for accessing the WebDAV server.
        /// </summary>
        internal static WebDavSession DavClient;

        static async Task Main(string[] args)
        {
            // Load Settings.
            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build();
            Settings = configuration.ReadSettings();

            // Load Log4Net for net configuration.
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            // Enable UTF8 for Console Window
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            log.Info($"\n{Process.GetCurrentProcess().ProcessName} {Settings.AppID}");
            log.Info($"\nOS version: {RuntimeInformation.OSDescription}.");
            log.Info($"\nEnv version: {RuntimeInformation.FrameworkDescription} {IntPtr.Size * 8}bit.");
            log.Info($"\nLog path: {(logRepository.GetAppenders().Where(p => p.GetType() == typeof(RollingFileAppender)).FirstOrDefault() as RollingFileAppender)?.File}.");
            log.Info("\nPress 'Q' to unregister file system, delete all files/folders and exit (simulate uninstall with full cleanup).");
            log.Info("\nPress 'q' to unregister file system and exit (simulate uninstall).");
            log.Info("\nPress any other key to exit without unregistering (simulate reboot).");
            log.Info("\n----------------------\n");

            // Typically you will register sync root during your application installation.
            // Here we register it during first program start for the sake of the development convenience.
            if (!await Registrar.IsRegisteredAsync(Settings.UserFileSystemRootPath))
            {
                log.Info($"\nRegistering {Settings.UserFileSystemRootPath} sync root.");
                Directory.CreateDirectory(Settings.UserFileSystemRootPath);
                Directory.CreateDirectory(Settings.ServerDataFolderPath);

                await Registrar.RegisterAsync(SyncRootId, Settings.UserFileSystemRootPath, Settings.ProductName,
                    Path.Combine(Settings.IconsFolderPath, "Drive.ico"));
            }
            else
            {
                log.Info($"\n{Settings.UserFileSystemRootPath} sync root already registered.");
            }

            // Log indexed state. Indexing must be enabled for the sync root to function.
            StorageFolder userFileSystemRootFolder = await StorageFolder.GetFolderFromPathAsync(Settings.UserFileSystemRootPath);
            log.Info($"\nIndexed state: {(await userFileSystemRootFolder.GetIndexedStateAsync())}\n");

            ConfigureWebDavSession();

            ConsoleKeyInfo? exitKey = null;

            // Event to be fired when any key will be pressed in the console or when the tray application exits.
            ConsoleManager.ConsoleExitEvent exitEvent = new ConsoleManager.ConsoleExitEvent();

            try
            {
                Engine = new VirtualEngine(
                    Settings.UserFileSystemLicense, 
                    Settings.UserFileSystemRootPath,                
                    Settings.ServerDataFolderPath,
                    Settings.WebSocketServerUrl,
                    Settings.IconsFolderPath, 
                    log);
                Engine.AutoLock = Settings.AutoLock;

                // Start tray application.
                Thread tryIconThread = WindowsTrayInterface.CreateTrayInterface(Settings.ProductName, Engine, exitEvent);

                // Start processing OS file system calls.
                await Engine.StartAsync();

#if DEBUG
                // Opens Windows File Manager with user file system folder and remote storage folder.
                ShowTestEnvironment();
#endif
                // Keep this application running until user input.
                ConsoleManager.WaitConsoleReadKey(exitEvent);

                // Wait until the "Exit" is pressed or any key in console is pressed to stop application.
                exitEvent.WaitOne();
                exitKey = exitEvent.KeyInfo;
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                Engine.Dispose();
            }

            if (exitKey?.KeyChar == 'q')
            {
                // Unregister during programm uninstall.
                await Registrar.UnregisterAsync(SyncRootId);
                log.Info($"\n\nUnregistering {Settings.UserFileSystemRootPath} sync root.");
                log.Info("\nAll empty file and folder placeholders are deleted. Hydrated placeholders are converted to regular files / folders.\n");
            }
            else if (exitKey?.KeyChar == 'Q')
            {
                log.Info($"\n\nUnregistering {Settings.UserFileSystemRootPath} sync root.");
                log.Info("\nAll files and folders placeholders are deleted.\n");

                // Unregister during programm uninstall and delete all files/folder.
                await Registrar.UnregisterAsync(SyncRootId);

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
                    Directory.Delete(Settings.ServerDataFolderPath, true);
                }
                catch (Exception ex)
                {
                    log.Error($"\n{ex}");
                }
            }
            else
            {
                log.Info("\n\nAll downloaded file / folder placeholders remain in file system. Restart the application to continue managing files.\n");
            }
        }

#if DEBUG
        /// <summary>
        /// Opens Windows File Manager with both remote storage and user file system for testing.
        /// </summary>
        /// <remarks>This method is provided solely for the development and testing convenience.</remarks>
        private static void ShowTestEnvironment()
        {
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

            // Open Windows File Manager with custom data storage. Uncomment this to debug custom data storage management.
            ProcessStartInfo serverDataInfo = new ProcessStartInfo(Program.Settings.ServerDataFolderPath);
            serverDataInfo.UseShellExecute = true; // Open window only if not opened already.
            using (Process serverDataWinFileManager = Process.Start(serverDataInfo))
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
        /// Creates and configures WebDAV client to access the remote storage;
        /// </summary>
        /// <remarks>Set authentication credentials if needed. Supports Basic, Digest, NTLM, Kerberos.
        /// DavClient.Credentials = new System.Net.NetworkCredential("User1", "pwd");
        ///
        /// Disable automatic redirect processing so we can process the 302 login redirect inside the error event handler.</remarks>
        private static void ConfigureWebDavSession(HttpClientHandler handler = null)
        {
            if (DavClient != null)
                DavClient.Dispose();
             
            DavClient = new WebDavSession(Program.Settings.WebDAVClientLicense, handler);

            DavClient.WebDavError += DavClient_WebDavError;
            DavClient.WebDavMessage += DavClient_WebDAVMessage;
        }

        /// <summary>
        /// Event handler to process WebDAV messages. 
        /// </summary>
        /// <param name="sender">Request to the WebDAV client.</param>
        /// <param name="e">WebDAV message details.</param>
        private static void DavClient_WebDAVMessage(ISession client, WebDavMessageEventArgs e)
        {
            string msg = $"\n{e.Message}";
            if (e.LogLevel == LogLevel.Debug)
                log.Debug($"{msg}\n");
            else
                log.Info(msg);
        }

        /// <summary>
        /// Maximum number of login attempts.
        /// </summary>
        private static uint loginRetriesMax = 3;

        /// <summary>
        /// Current login attempt.
        /// </summary>
        private static uint loginRetriesCurrent = 0;

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
                                DavClient.Credentials = new NetworkCredential(passwordCredential.UserName, passwordCredential.Password);
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
                                    DavClient.Credentials = new NetworkCredential(login, password);
                                    e.Result = WebDavErrorEventResult.Repeat;
                                }
                            }
                        }
                        break;
                }
            }
        }
    }
}
