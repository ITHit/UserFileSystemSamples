using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.WebDAV.Client;
using ITHit.WebDAV.Client.Exceptions;
using log4net;
using log4net.Config;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VirtualFileSystem.Syncronyzation;
using Windows.Storage;
using Windows.Storage.Provider;

namespace VirtualFileSystem
{
    class Program
    {
        /// <summary>
        /// Application settings.
        /// </summary>
        internal static Settings Settings;

        /// <summary>
        /// WebDAV client for accessing the WebDAV server.
        /// </summary>
        internal static WebDavSessionAsync DavClient;

        /// <summary>
        /// Log4Net logger.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Processes file system calls, implements on-demand loading and initial data transfer from remote storage to client.
        /// </summary>
        private static VfsEngine engine;

        /// <summary>
        /// Monitores changes in the remote file system.
        /// </summary>
        internal static RemoteStorageMonitor RemoteStorageMonitorInstance;

        /// <summary>
        /// Monitors pinned and unpinned attributes in user file system.
        /// </summary>
        private static UserFileSystemMonitor userFileSystemMonitor;

        /// <summary>
        /// Performs complete synchronyzation of the folders and files that are already synched to user file system.
        /// </summary>
        private static FullSyncService syncService;

        //[STAThread]
        static async Task<int> Main(string[] args)
        {
            // Load Settings.
            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build();
            Settings = configuration.ReadSettings();

            // Load Log4Net for net configuration.
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            log.Info($"\n{Settings.ProductName}");
            log.Info("\nPress 'Q' to unregister file system, delete all files/folders and exit (simulate uninstall with full cleanup).");
            log.Info("\nPress 'q' to unregister file system and exit (simulate uninstall).");
            log.Info("\nPress any other key to exit without unregistering (simulate reboot).");
            log.Info("\n----------------------\n");

            // Typically you will register sync root during your application installation.
            // Here we register it during first program start for the sake of the development convenience.
            if (!await Registrar.IsRegisteredAsync(Settings.UserFileSystemRootPath))
            {
                Directory.CreateDirectory(Settings.UserFileSystemRootPath);
                log.Info($"\nRegistering {Settings.UserFileSystemRootPath} sync root.");
                
                
                await Registrar.RegisterAsync(SyncRootId, Settings.UserFileSystemRootPath, Settings.ProductName);
            }
            else
            {
                log.Info($"\n{Settings.UserFileSystemRootPath} sync root already registered.");
            }

            // Log indexed state.
            StorageFolder userFileSystemRootFolder = await StorageFolder.GetFolderFromPathAsync(Settings.UserFileSystemRootPath);
            log.Info($"\nIndexed state: {(await userFileSystemRootFolder.GetIndexedStateAsync())}\n");

            ConfigureWebDAVClient();

            ConsoleKeyInfo exitKey;

            try
            {
                engine = new VfsEngine(Settings.UserFileSystemLicense, Settings.UserFileSystemRootPath, log);
                RemoteStorageMonitorInstance = new RemoteStorageMonitor(Settings.WebDAVServerUrl, log);
                syncService = new FullSyncService(Settings.SyncIntervalMs, Settings.UserFileSystemRootPath, log);
                userFileSystemMonitor = new UserFileSystemMonitor(Settings.UserFileSystemRootPath, log);

                // Start processing OS file system calls.
                //engine.ChangesProcessingEnabled = false;
                await engine.StartAsync();

                // Start monitoring changes in remote file system.
                //await RemoteStorageMonitorInstance.StartAsync();

                // Start periodical synchronyzation between client and server, 
                // in case any changes are lost because the client or the server were unavailable.
                await syncService.StartAsync();

                // Start monitoring pinned/unpinned attributes and files/folders creation in user file system.
                await userFileSystemMonitor.StartAsync();
#if DEBUG
                // Opens Windows File Manager with user file system folder and remote storage folder.
                ShowTestEnvironment();
#endif
                // Keep this application running until user input.
                exitKey = Console.ReadKey();
            }
            finally
            {
                engine.Dispose();
                RemoteStorageMonitorInstance.Dispose();
                syncService.Dispose();
                userFileSystemMonitor.Dispose();
            }

            if (exitKey.KeyChar == 'q')
            {
                // Unregister during programm uninstall.
                await Registrar.UnregisterAsync(SyncRootId);
                log.Info($"\n\nUnregistering {Settings.UserFileSystemRootPath} sync root.");
                log.Info("\nAll empty file and folder placeholders are deleted. Hydrated placeholders are converted to regular files / folders.\n");
            }
            else if (exitKey.KeyChar == 'Q')
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
            }
            else
            {
                log.Info("\n\nAll downloaded file / folder placeholders remain in file system. Restart the application to continue managing files.\n");
            }

            return 1;
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


            // Open Windows File Manager with ETags and locks storage.
            //ProcessStartInfo serverDataInfo = new ProcessStartInfo(Program.Settings.ServerDataFolderPath);
            //serverDataInfo.UseShellExecute = true; // Open window only if not opened already.
            //using (Process serverDataWinFileManager = Process.Start(serverDataInfo))
            //{

            //}
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
                return $"{System.Diagnostics.Process.GetCurrentProcess().ProcessName}!{System.Security.Principal.WindowsIdentity.GetCurrent().User}!User";
            }
        }

        /// <summary>
        /// Creates and configures WebDAV client to access the remote storage;
        /// </summary>
        private static void ConfigureWebDAVClient()
        {
            DavClient = new WebDavSessionAsync(Program.Settings.WebDAVClientLicense);
            
            // Set authentication credentials if needed. Supports Basic, Digest, NTLM, Kerberos.
            // DavClient.Credentials = new System.Net.NetworkCredential("User1", "pwd");

            // Disable automatic redirect processing so we can process the 
            // 302 login redirect inside the error event handler.
            DavClient.AllowAutoRedirect = false;
            DavClient.WebDavError += DavClient_WebDavError;

            ITHit.WebDAV.Client.Logger.FileLogger.LogFile = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().CodeBase), "WebDAVLog.txt");
            ITHit.WebDAV.Client.Logger.FileLogger.Level = ITHit.WebDAV.Client.Logger.LogLevel.All;
        }

        /// <summary>
        /// Maximum number of login attempts.
        /// </summary>
        private static uint loginRetriesMax = 3;

        /// <summary>
        /// Current login attempt.
        /// </summary>
        private static uint loginRetriesCurrent = 0;


        private static void DavClient_WebDavError(IWebRequestAsync sender, WebDavErrorEventArgs e)
        {
            WebDavHttpException httpException = e.Exception as WebDavHttpException;
            log.Info($"\n{httpException?.Status.Code} {httpException?.Status.Description} {e.Exception.Message} ");
            if (httpException != null)
            {
                switch (httpException.Status.Code)
                {
                    // 302 redirect to login page.
                    case 302 :
                        if (loginRetriesCurrent < loginRetriesMax)
                        {
                            // Show login dialog.

                            // Azure AD can not navigate directly to login page - failed corelation.
                            //string loginUrl = ((Redirect302Exception)e.Exception).Location;
                            //Uri url = new System.Uri(loginUrl, System.UriKind.Absolute);

                            Uri failedUri = (e.Exception as WebDavHttpException).Uri;

                            WebDAVDrive.Login.WebBrowserLogin loginForm = null;
                            Thread thread = new Thread(() => {
                                loginForm = new WebDAVDrive.Login.WebBrowserLogin(failedUri, e.Request, DavClient);
                                Application.Run(loginForm);
                            });
                            thread.SetApartmentState(ApartmentState.STA);
                            thread.Start();
                            thread.Join();

                            loginRetriesCurrent++;
                            
                            /*
                            if (loginForm.Cookies != null)
                            {
                                // Attach cookies to all future requests.
                                DavClient.CookieContainer.Add(loginForm.Cookies);
                                e.Result = WebDavErrorEventResult.Fail;

                                // Set successful response and continue processing.
                                e.Response = loginForm.Response;
                                e.Result = WebDavErrorEventResult.ContinueProcessing;

                                // Alternatively you can modify this request, attaching cookies or headers, and replay it.
                                //e.Request.CookieContainer.Add(loginForm.Cookies);
                                //e.Result = WebDavErrorEventResult.Repeat;
                            }
                            */
                        }
                        break;

                        // Challenge-responce auth: Basic, Digest, NTLM or Kerberos
                    case 401:
                        if (loginRetriesCurrent < loginRetriesMax)
                        {
                            // Show login dialog.

                            Uri failedUri = (e.Exception as WebDavHttpException).Uri;

                            WebDAVDrive.Login.ChallengeLogin loginForm = null;
                            Thread thread = new Thread(() => {
                                loginForm = new WebDAVDrive.Login.ChallengeLogin();
                                loginForm.Server.Text = failedUri.OriginalString;
                                Application.Run(loginForm);
                            });
                            thread.SetApartmentState(ApartmentState.STA);
                            thread.Start();
                            thread.Join();

                            loginRetriesCurrent++;

                            if (loginForm.DialogResult == DialogResult.OK)
                            {
                                string login = loginForm.Login.Text.Trim();
                                string password = loginForm.Password.Text.Trim();
                                DavClient.Credentials = new NetworkCredential(login, password);
                            }
                        }
                        break;
                }
            }
        }
    }
}
