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
using Windows.Storage;
using Windows.Storage.Provider;

using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;

namespace VirtualFileSystem
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
        /// synchronizes user file system to remote storage and back, 
        /// monitors files pinning and unpinning.
        /// </summary>
        internal static VirtualDrive VirtualDrive;

        static async Task<int> Main(string[] args)
        {
            // Load Settings.
            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build();
            Settings = configuration.ReadSettings();

            // Load Log4Net for net configuration.
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            // Enable UTF8 for Console Window
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            log.Info($"\n{System.Diagnostics.Process.GetCurrentProcess().ProcessName} {Settings.AppID}");
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

            // Log indexed state.
            StorageFolder userFileSystemRootFolder = await StorageFolder.GetFolderFromPathAsync(Settings.UserFileSystemRootPath);
            log.Info($"\nIndexed state: {(await userFileSystemRootFolder.GetIndexedStateAsync())}\n");

            ConsoleKeyInfo exitKey;

            try
            {
                VirtualDrive = new VirtualDrive(Settings.UserFileSystemLicense, Settings.UserFileSystemRootPath, Settings, log);

                // Start processing OS file system calls, monitoring changes in user file system and remote storge.
                //engine.ChangesProcessingEnabled = false;
                await VirtualDrive.StartAsync();
                await VirtualDrive.SyncService.StopAsync();

#if DEBUG
                // Opens Windows File Manager with user file system folder and remote storage folder.
                ShowTestEnvironment();
#endif
                // Keep this application running until user input.
                exitKey = Console.ReadKey();
            }
            finally
            {
                VirtualDrive.Dispose();
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

            // Open Windows File Manager with remote storage.
            ProcessStartInfo rsInfo = new ProcessStartInfo(Program.Settings.RemoteStorageRootPath);
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
                return $"{Settings.AppID}!{System.Security.Principal.WindowsIdentity.GetCurrent().User}!User";
            }
        }
    }
}
