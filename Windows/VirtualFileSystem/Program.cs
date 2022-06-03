using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

using log4net;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;


namespace VirtualFileSystem
{
    public class Program
    {
        /// <summary>
        /// Application settings.
        /// </summary>
        private static AppSettings Settings;

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
        /// Outputs logging information.
        /// </summary>
        private static LogFormatter logFormatter;

        public static async Task Main(string[] args)
        {
            // Load Settings.
            Settings = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build().ReadSettings();

            logFormatter = new LogFormatter(log, Settings.AppID);

            try
            {
                // Log environment description.
                logFormatter.PrintEnvironmentDescription();

                // Register sync root and create app folders.
                await RegisterSyncRootAsync();

                // Log indexing state. Sync root must be indexed.
                await logFormatter.PrintIndexingStateAsync(Settings.UserFileSystemRootPath);

                // Log console commands.
                logFormatter.PrintHelp();

                // Log logging columns headers.
                logFormatter.PrintHeader();

                using (Engine = new VirtualEngine(
                    Settings.UserFileSystemLicense,
                    Settings.UserFileSystemRootPath,
                    Settings.RemoteStorageRootPath,
                    logFormatter))
                {
                    // Set the remote storage item ID for the root item. It will be passed to the IEngine.GetFileSystemItemAsync()
                    // method as a remoteStorageItemId parameter when a root folder is requested. 
                    byte[] itemId = WindowsFileSystemItem.GetItemIdByPath(Settings.RemoteStorageRootPath);
                    Engine.Placeholders.GetRootItem().SetRemoteStorageItemId(itemId);

                    // Start processing OS file system calls.
                    await Engine.StartAsync();

#if DEBUG
                    // Opens Windows File Manager with user file system folder and remote storage folder.
                    ShowTestEnvironment();
#endif
                    // Keep this application running and reading user input.
                    await ProcessUserInputAsync();
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                await ProcessUserInputAsync();
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
                            Engine.RemoteStorageMonitor.Start();
                        }
                        else
                        {
                            Engine.RemoteStorageMonitor.Stop();
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
                        if (Engine.State == EngineState.Running)
                        {
                            await Engine.StopAsync();
                        }

                        // Call the code below during programm uninstall using classic msi.
                        await UnregisterSyncRootAsync();

                        // Delete all files/folders.
                        await CleanupAppFoldersAsync();
                        return;

                    case ConsoleKey.Spacebar:
                        if (Engine.State == EngineState.Running)
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
                log.Info($"\n\nRegistering {Settings.UserFileSystemRootPath} sync root.");
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

            // Open Windows File Manager with remote storage.
            ProcessStartInfo rsInfo = new ProcessStartInfo(Program.Settings.RemoteStorageRootPath);
            rsInfo.UseShellExecute = true; // Open window only if not opened already.
            using (Process rsWinFileManager = Process.Start(rsInfo))
            {

            }

            // Open Windows File Manager with user file system.
            ProcessStartInfo ufsInfo = new ProcessStartInfo(Program.Settings.UserFileSystemRootPath);
            ufsInfo.UseShellExecute = true; // Open window only if not opened already.
            using (Process ufsWinFileManager = Process.Start(ufsInfo))
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
