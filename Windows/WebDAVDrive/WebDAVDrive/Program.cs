using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

using log4net;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Windows.Package;
using ITHit.FileSystem.Samples.Common.Windows;

using WebDAVDrive.UI;


namespace WebDAVDrive
{
    /// <summary>
    /// - To run this app with a sparse package application identity run this project directly from Virual Studio in debug mode.
    /// This will install the dev certificate and register sparse package. Then restart the project to use the app.
    /// Shell extensions are installed/ununstalled automatically via sparse package manifest located in this project.
    /// 
    /// - To run this app with a package identity run the .Package project provided with this sample.
    /// Packaged application enables deployment to Microsoft Store.
    /// Shell extensions are installed/ununstalled automatically via a manifest located in the .Package project.
    /// 
    /// - To run this app without identity comment out the <see cref="SparsePackageInstaller.RegisterSparsePackageAsync"/> call in
    /// this class and run this project.
    /// Shell extensions are installed/uninstalled using the <see cref="Registrar"/> class, when sync root is registered/unregistered.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Engine instances. 
        /// Each item contains Engine instance ID and engine itself.
        /// Instance ID is used to delete the Engine from this list if file system is unmounted.
        /// </summary>
        
        public static ConcurrentDictionary<Guid, EngineWindows> Engines = new ConcurrentDictionary<Guid, EngineWindows>();

        /// <summary>
        /// Application settings.
        /// </summary>
        internal static AppSettings Settings;

        /// <summary>
        /// Log4Net logger.
        /// </summary>
        internal static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Outputs logging information.
        /// </summary>
        private static LogFormatter logFormatter;

        /// <summary>
        /// Provides sync root registration functionality and sparse package installation.
        /// </summary>
        private static SparsePackageRegistrar registrar;

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

            registrar = new SparsePackageRegistrar(Log, ShellExtension.ShellExtensions.Handlers);
            consoleProcessor = new ConsoleProcessor(registrar, logFormatter, Settings.AppID);

            // Print console commands.
            consoleProcessor.PrintHelp();

            TrayUI trayUI = null;
            try
            {
                ValidatePackagePrerequisites();

                // Install dev cert and register sparse package.
                if (!await registrar.RegisterSparsePackageAsync())
                {
                    return; // Sparse package registered - restart the sample.
                }

                // Start console input processing.
                Task taskConsole = StartConsoleReadKeyAsync();

                // Start tray processing.
                trayUI = new TrayUI();
                Task taskTrayUI = trayUI.StartAsync();

                // Register this app to process COM shell extensions calls.
                using (ShellExtension.ShellExtensions.StartComServer(Settings.ShellExtensionsComServerRpcEnabled))
                {
                    
                    // Read mounted file systems.
                    var syncRoots = await Registrar.GetMountedSyncRootsAsync(Settings.AppID, Log);
                    if (!syncRoots.Any())
                    {
                        // This is first start. Mount file system roots from settings.
                        await MountNewAsync(Settings.WebDAVServerURLs);
                    }
                    else
                    {
                        // Roots were lready mountied during previous runs. This is app restart or reboot.
                        await RunExistingAsync(syncRoots);
                    }

                    // Wait for console or all tray apps exit.
                    //System.Windows.Forms.Application.Run();

                    Task.WaitAny(taskConsole, taskTrayUI);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"\n\n Press Shift-Esc to fully uninstall the app. Then start the app again.\n\n", ex);
                await consoleProcessor.ProcessUserInputAsync();
            }
            finally
            {
                trayUI?.Dispose();

                foreach (var keyValue in Engines)
                {
                    keyValue.Value?.Dispose();
                }
            }
        }

        private static async Task RunExistingAsync(IEnumerable<Windows.Storage.Provider.StorageProviderSyncRootInfo> syncRoots)
        {
            foreach (var syncRoot in syncRoots)
            {
                string webDAVServerUrl = syncRoot.GetRemoteStoragePath();
                // Run the User File System Engine.
                await TryCreateEngineAsync(webDAVServerUrl, syncRoot.Path.Path);
            }
        }

        private static async Task MountNewAsync(string[] webDAVServerURLs)
        {
            // Mount new file system for each URL, run Engine and tray app.
            foreach (string webDAVServerUrl in webDAVServerURLs)
            {
                // Register sync root and run User File System Engine.
                await TryMountNewAsync(webDAVServerUrl);
            }
        }

        private static async Task<bool> TryMountNewAsync(string webDAVServerUrl)
        {
            string userFileSystemRootPath = null;
            try
            {
                userFileSystemRootPath = GenerateRootPathForProtocolMounting();
                string displayName = GetDisplayName(webDAVServerUrl);

                // Register sync root and create app folders.
                await registrar.RegisterSyncRootAsync(
                    GetSyncRootId(webDAVServerUrl),
                    userFileSystemRootPath,
                    webDAVServerUrl,
                    displayName,
                    Path.Combine(Settings.IconsFolderPath, "Drive.ico"),
                    Settings.ShellExtensionsComServerExePath);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to mount file system {webDAVServerUrl} {userFileSystemRootPath}", ex);
                return false;
            }
            // Run the User File System Engine.
            return await TryCreateEngineAsync(webDAVServerUrl, userFileSystemRootPath);
        }

        private static string GetDisplayName(string webDAVServerUrl)
        {
            return webDAVServerUrl.Remove(0, "https://".Length);
        }

        private static string GenerateRootPathForProtocolMounting()
        {
            string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string randomName = Path.GetRandomFileName();
            string folderName = Path.GetFileNameWithoutExtension(randomName);
            return Path.Combine(userProfilePath, "DAV", folderName);            
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

        private static async Task<bool> TryCreateEngineAsync(string webDAVServerUrl, string userFileSystemRootPath)
        {
            try
            {
                Uri webDAVServer = new Uri(webDAVServerUrl);
                string webSocketsProtocol = webDAVServer.Scheme == "https" ? "wss" : "ws";
                string webSocketServerUrl = $"{webSocketsProtocol}://{webDAVServer.Authority}/";
                
                VirtualEngine engine = new VirtualEngine(
                    Settings.UserFileSystemLicense,
                    userFileSystemRootPath,
                    webDAVServerUrl,
                    webSocketServerUrl,
                    Settings.IconsFolderPath,
                    Settings.AutoLockTimoutMs,
                    Settings.ManualLockTimoutMs,
                    Settings.SetLockReadOnly,
                    logFormatter,
                    Settings.ProductName);

                Engines.TryAdd(engine.InstanceId, engine);
                //engine.Tray.MenuExit.Click += async (object sender, EventArgs e) => { await RemoveEngineAsync(engine, false); };
                //engine.Tray.MenuUnmount.Click += async (object sender, EventArgs e) => { await RemoveEngineAsync(engine, true); };

                consoleProcessor.Commands.TryAdd(engine.InstanceId, engine.Commands);

                engine.SyncService.SyncIntervalMs = Settings.SyncIntervalMs;
                engine.SyncService.IncomingSyncMode = Settings.PreferredIncomingSyncMode;
                engine.AutoLock = Settings.AutoLock;
                engine.MaxTransferConcurrentRequests = Settings.MaxTransferConcurrentRequests.Value;
                engine.MaxOperationsConcurrentRequests = Settings.MaxOperationsConcurrentRequests.Value;
                engine.ShellExtensionsComServerRpcEnabled = Settings.ShellExtensionsComServerRpcEnabled; // Enable RPC in case RPC shaell extension handlers, hosted in separate process. 

                // Print Engine config, settings, logging headers.
                await logFormatter.PrintEngineStartInfoAsync(engine, webDAVServerUrl);

#if DEBUG
                Commands.Open(webDAVServerUrl); // Open remote storage.
#endif
                // Start processing OS file system calls.
                await engine.StartAsync();
#if DEBUG
                // Start Windows File Manager with user file system folder.
                engine.Commands.ShowTestEnvironment(GetDisplayName(webDAVServerUrl), false, default, Engines.Count-1, Settings.WebDAVServerURLs.Count());
#endif
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to start Engine {webDAVServerUrl} {userFileSystemRootPath}", ex);
                return false;
            }
        }

        private static async Task StartConsoleReadKeyAsync()
        {
            await Task.Run(async () => await consoleProcessor.ProcessUserInputAsync());
        }

        /// <summary>
        /// Gets automatically generated Sync Root ID.
        /// </summary>
        /// <remarks>An identifier in the form: [Storage Provider ID]![Windows SID]![Account ID]</remarks>
        private static string GetSyncRootId(string remoteStoragePathRoot)
        {
            return $"{Settings.AppID}!{System.Security.Principal.WindowsIdentity.GetCurrent().User}!{remoteStoragePathRoot}!User";
        }

        /// <summary>
        /// Stops the Engine and removes it from the list of running engines, exits tray app, 
        /// exit app if engine list is empty. Optionally unmount sync root.
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        /// <param name="unregisterSyncRoot">Pass true to unmount sync root.</param>
        public static async Task RemoveEngineAsync(VirtualEngine engine, bool unregisterSyncRoot)
        {
            Log.Info($"\n\nRemoving {engine.RemoteStorageRootPath}");
            if (!unregisterSyncRoot)
            {
                Log.Info("\nAll downloaded file / folder placeholders remain in file system. Restart the application to continue managing files.");
                Log.Info("\nYou can edit documents when the app is not running and than start the app to sync all changes to the remote storage.\n");
            }
            else
            {
                Log.Info("\nAll downloaded file / folder are deleted.");
            }

            // Stop Engine.
            if (engine?.State == EngineState.Running)
            {
                await engine.StopAsync();
            }

            Engines.TryRemove(engine.InstanceId, out _);
            consoleProcessor.Commands.TryRemove(engine.InstanceId, out _);

            // Unmount sync root.
            if (unregisterSyncRoot)
            {
                await Registrar.UnregisterSyncRootAsync(engine.Path, engine.DataPath, Program.Log);
            }
            engine.Dispose();

            // Refresh Windows Explorer to remove the root node.
            PlaceholderItem.UpdateUI(Path.GetDirectoryName(engine.Path));

            // If no Engines are running exit the app.
            if (!Engines.Any())
            {
                System.Windows.Forms.Application.Exit();
            }
        }
    }
}
