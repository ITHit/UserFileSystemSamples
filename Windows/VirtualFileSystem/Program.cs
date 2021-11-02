using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage;

using log4net;
using log4net.Appender;
using log4net.Config;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;

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
        /// Log file path.
        /// </summary>
        private static string LogFilePath;

        /// <summary>
        /// Processes OS file system calls, 
        /// synchronizes user file system to remote storage. 
        /// </summary>
        public static VirtualEngine Engine;

        static async Task Main(string[] args)
        {
            // Load Settings.
            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build();
            Settings = configuration.ReadSettings();

            // Configure log4net and set log file path.
            LogFilePath = ConfigureLogger();

            // Enable UTF8 for Console Window.
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            PrintHelp();

            // Register sync root and create app folders.
            await RegisterSyncRootAsync();

            // Log indexed state.
            StorageFolder userFileSystemRootFolder = await StorageFolder.GetFolderFromPathAsync(Settings.UserFileSystemRootPath);
            log.Info($"\nIndexed state: {(await userFileSystemRootFolder.GetIndexedStateAsync())}\n");

            Logger.PrintHeader(log);

            // Set root item ID. It will be passed to IEngine.GetFileSystemItemAsync() method 
            // as itemId parameter when a root folder is requested. 
            byte[] itemId = WindowsFileSystemItem.GetItemIdByPath(Settings.RemoteStorageRootPath);
            PlaceholderFolder.GetItem(Settings.UserFileSystemRootPath).SetItemId(itemId);

            try
            {
                Engine = new VirtualEngine(
                    Settings.UserFileSystemLicense, 
                    Settings.UserFileSystemRootPath, 
                    Settings.RemoteStorageRootPath, 
                    log);

                // Start processing OS file system calls.
                await Engine.StartAsync();

#if DEBUG
                // Opens Windows File Manager with user file system folder and remote storage folder.
                ShowTestEnvironment();
#endif
                // Keep this application running and reading user input.
                await ProcessUserInputAsync();
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
            //log.Info("\nPress 's' to start/stop full synchronization service.");
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

                    case 'm':
                        // Start/stop remote storage monitor.
                        if (Engine.RemoteStorageMonitor.SyncState == SynchronizationState.Disabled)
                        {
                            if (Engine.State != EngineState.Running)
                            {
                                Engine.RemoteStorageMonitor.LogError("Failed to start. The Engine must be running.");
                                break;
                            }
                            Engine.RemoteStorageMonitor.Start();
                        }
                        else
                        {
                            Engine.RemoteStorageMonitor.Stop();
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
                        // Unregister during programm uninstall.                        
                        Engine.Dispose();
                        await UnregisterSyncRootAsync();

                        // Delete all files/folders.
                        CleanupAppFolders();
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
        }

        private static void CleanupAppFolders()
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
                string localApplicationDataFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appDataPath = Path.Combine(localApplicationDataFolderPath, Settings.AppID);
                Directory.Delete(appDataPath, true);
            }
            catch (Exception ex)
            {
                log.Error($"\n{ex}");
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

            // Open Windows File Manager with remote storage.
            ProcessStartInfo rsInfo = new ProcessStartInfo(Program.Settings.RemoteStorageRootPath);
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
