using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using log4net;
using log4net.Config;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
        private static SyncService syncService;

        static async Task<int> Main(string[] args)
        {
            // Load Settings.
            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build();
            Settings = configuration.ReadSettings();

            // Load Log4Net for net configuration.
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            log.Info($"\n{System.Diagnostics.Process.GetCurrentProcess().ProcessName}");
            log.Info("\nPress 'q' to unregister file system and exit (simulate uninstall).");
            log.Info("\nPress any other key to exit without unregistering (simulate reboot).");
            log.Info("\n----------------------\n");

            // Typically you will register sync root during your application installation.
            // Here we register it during first program start for the sake of the development convenience.
            if (!await Registrar.IsRegisteredAsync(Settings.UserFileSystemRootPath))
            {
                Directory.CreateDirectory(Settings.UserFileSystemRootPath);
                log.Info($"\nRegistering {Settings.UserFileSystemRootPath} sync root.");
                await Registrar.RegisterAsync(SyncRootId, Settings.UserFileSystemRootPath, "My Virtual File System");
            }
            else
            {
                log.Info($"\n{Settings.UserFileSystemRootPath} sync root already registered.");
            }

            // Log indexed state.
            StorageFolder userFileSystemRootFolder = await StorageFolder.GetFolderFromPathAsync(Settings.UserFileSystemRootPath);
            log.Info($"\nIndexed state: {(await userFileSystemRootFolder.GetIndexedStateAsync())}\n");

            ConsoleKeyInfo exitKey;

            try
            {
                engine = new VfsEngine(Settings.License, Settings.UserFileSystemRootPath, log);
                RemoteStorageMonitorInstance = new RemoteStorageMonitor(Settings.RemoteStorageRootPath, log);
                syncService = new SyncService(Settings.SyncIntervalMs, Settings.UserFileSystemRootPath, log);
                userFileSystemMonitor = new UserFileSystemMonitor(Settings.UserFileSystemRootPath, log);

                // Start processing OS file system calls.
                //engine.ChangesProcessingEnabled = false;
                await engine.StartAsync();

                // Start monitoring changes in remote file system.
                await RemoteStorageMonitorInstance.StartAsync();

                // Start periodical synchronyzation between client and server, 
                // in case any changes are lost because the client or the server were unavailable.
                await syncService.StartAsync();

                // Start monitoring pinned/unpinned attributes and files/folders creation in user file system.
                await userFileSystemMonitor.StartAsync();
#if DEBUG
                // Opens Windows File Manager with user file system folder and remote storage folder.
                ShowTestEnvironment(Settings.UserFileSystemRootPath, Settings.RemoteStorageRootPath);
#endif
                // Keep this application running untill user input.
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
        /// <param name="userFileSystemRootPath">User file system path.</param>
        /// <param name="remoteStorageRootPath">Remote storage path.</param>
        private static void ShowTestEnvironment(string userFileSystemRootPath, string remoteStorageRootPath)
        {
            // Open Windows File Manager with user file system.
            ProcessStartInfo ufsInfo = new ProcessStartInfo(userFileSystemRootPath);
            ufsInfo.UseShellExecute = true; // Open window only if not opened already.
            using (Process ufsWinFileManager = Process.Start(ufsInfo))
            {

            }

            // Open Windows File Manager with remote storage.
            ProcessStartInfo rsInfo = new ProcessStartInfo(remoteStorageRootPath);
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
                return $"{System.Diagnostics.Process.GetCurrentProcess().ProcessName}!{System.Security.Principal.WindowsIdentity.GetCurrent().User}!User";
            }
        }

        internal static string IconsFolderPath
        {
            get
            {
                return Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), @"Images");
            }
        }
    }
}
