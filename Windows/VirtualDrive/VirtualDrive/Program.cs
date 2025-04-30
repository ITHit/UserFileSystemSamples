using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

using log4net;

using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows.Package;
using ITHit.FileSystem;
using Microsoft.VisualBasic.Logging;

namespace VirtualDrive
{
    /// <summary>
    /// - To run this app with a sparse package application identity run this project directly from Visual Studio in debug mode.
    /// This will install the dev certificate and register a sparse package. Then restart the project to use the app.
    /// Shell extensions are installed/uninstalled automatically via a sparse package manifest located in this project.
    /// 
    /// - To run this app with a package identity run the .Package project provided with this sample.
    /// Packaged application enables deployment to Microsoft Store.
    /// Shell extensions are installed/uninstalled automatically via a manifest located in the .Package project.
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
        private static AppSettings Settings;

        /// <summary>
        /// Log4Net logger.
        /// </summary>
        internal static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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

        static async Task Main(string[] args)
        {
            // Load Settings.
            Settings = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build().ReadSettings();

            logFormatter = new LogFormatter(Log, Settings.AppID);
            WindowManager.ConfigureConsole();

            // Log environment description.
            logFormatter.PrintEnvironmentDescription();

            registrar = new SparsePackageRegistrar(Log, ShellExtension.ShellExtensions.Handlers);
            consoleProcessor = new ConsoleProcessor(registrar, logFormatter, Settings.AppID);

            switch (args.FirstOrDefault())
            {
#if DEBUG
                case "-InstallDevCert":
                    /// Called by <see cref="CertificateRegistrar.TryInstallCertificate"/> in elevated mode.
                    SparsePackageRegistrar.EnsureDevelopmentCertificateInstalled(Log);
                    return;

                case "-UninstallDevCert":
                    /// Called by <see cref="CertificateRegistrar.TryUninstallCertificate"/> in elevated mode.
                    SparsePackageRegistrar.EnsureDevelopmentCertificateUninstalled(Log);
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
                using (ShellExtension.ShellExtensions.StartComServer())
                {
                    // Run the User File System Engine.
                    await RunEngineAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"\n\n Press Shift-Esc to fully uninstall the app. Then start the app again.\n\n", ex);
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

        private static async Task RunEngineAsync()
        {
            // Register sync root and create app folders.
            await registrar.RegisterSyncRootAsync(
                SyncRootId,
                Settings.UserFileSystemRootPath,
                Settings.RemoteStorageRootPath,
                Settings.ProductName, 
                Path.Combine(Settings.IconsFolderPath, "Drive.ico"), Settings.CustomColumns);

            using (Engine = new VirtualEngine(
                    Settings.UserFileSystemLicense,
                    Settings.UserFileSystemRootPath,
                    Settings.RemoteStorageRootPath,
                    Settings.IconsFolderPath,
                    logFormatter))
            {
                commands = new Commands(Engine, Settings.RemoteStorageRootPath, Log);
                commands.RemoteStorageMonitor = Engine.RemoteStorageMonitor;
                consoleProcessor.Commands.TryAdd(Guid.Empty, commands);

                // Here we disable incoming sync. To get changes using pooling call IncomingPooling.ProcessAsync()
                Engine.SyncService.IncomingSyncMode = ITHit.FileSystem.Synchronization.IncomingSyncMode.Disabled;

                Engine.SyncService.SyncIntervalMs = Settings.SyncIntervalMs;
                Engine.AutoLock = Settings.AutoLock;
                Engine.MaxTransferConcurrentRequests = Settings.MaxTransferConcurrentRequests.Value;
                Engine.MaxOperationsConcurrentRequests = Settings.MaxOperationsConcurrentRequests.Value;

                // Set the remote storage item ID for the root item. It will be passed to the IEngine.GetFileSystemItemAsync()
                // method as a remoteStorageItemId parameter when a root folder is requested.
                byte[] itemId = WindowsFileSystemItem.GetItemIdByPath(Settings.RemoteStorageRootPath);
                Engine.SetRemoteStorageRootItemId(itemId);

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
