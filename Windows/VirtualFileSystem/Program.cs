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
using System.Threading;


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

        /// <summary>
        /// Provides sync root registration functionality.
        /// </summary>
        private static Registrar registrar;

        /// <summary>
        /// Application commands.
        /// </summary>
        private static Commands commands;

        /// <summary>
        /// Processes console input.
        /// </summary>
        private static ConsoleProcessor consoleProcessor;

        
        public static async Task Main(string[] args)
        {
            // Load Settings.
            Settings = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build().ReadSettings();

            logFormatter = new LogFormatter(log, Settings.AppID);
            WindowManager.ConfigureConsole();

            // Log environment description.
            logFormatter.PrintEnvironmentDescription();

            registrar = new Registrar(log);
            consoleProcessor = new ConsoleProcessor(registrar, logFormatter, Settings.AppID);

            try
            {
                // Register sync root and create app folders.
                await registrar.RegisterSyncRootAsync(
                    SyncRootId, 
                    Settings.UserFileSystemRootPath, 
                    Settings.RemoteStorageRootPath,
                    Settings.ProductName, 
                    Path.Combine(Settings.IconsFolderPath, "Drive.ico"));

                using (Engine = new VirtualEngine(
                    Settings.UserFileSystemLicense,
                    Settings.UserFileSystemRootPath,
                    Settings.RemoteStorageRootPath,
                    logFormatter))
                {
                    commands = new Commands(Engine, Settings.RemoteStorageRootPath, log);
                    commands.RemoteStorageMonitor = Engine.RemoteStorageMonitor;
                    consoleProcessor.Commands.TryAdd(Guid.Empty, commands);

                    // Here we disable incoming sync. To get changes from your remote storage using pooling, call the IncomingPooling.ProcessAsync() method.
                    Engine.SyncService.IncomingSyncMode = ITHit.FileSystem.Synchronization.IncomingSyncMode.Disabled;

                    // Set the remote storage item ID for the root item. It will be passed to the IEngine.GetFileSystemItemAsync()
                    // method as a remoteStorageItemId parameter when a root folder is requested. 
                    // In this sample we do not set the ID becuse in case of a network path the ID is not available.
                    //Engine.SetRemoteStorageRootItemId(rootItemId);

                    // Print console commands.
                    consoleProcessor.PrintHelp();

                    // Print Engine config, settings, logging headers.
                    await logFormatter.PrintEngineStartInfoAsync(Engine, Settings.RemoteStorageRootPath);

                    // Start processing OS file system calls.
                    await Engine.StartAsync();

                    // Sync all changes from remote storage one time for demo purposes.
                    await Engine.SyncService.IncomingPooling.ProcessAsync();
#if DEBUG
                    // Opens Windows File Manager with user file system folder and remote storage folder.
                    commands.ShowTestEnvironment(Settings.ProductName);
#endif
                    // Keep this application running and reading user input.
                    await consoleProcessor.ProcessUserInputAsync();
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                await consoleProcessor.ProcessUserInputAsync();
            }
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
