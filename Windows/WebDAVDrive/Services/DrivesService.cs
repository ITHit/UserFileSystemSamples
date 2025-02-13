using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Provider;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Windows.Package;
using ITHit.FileSystem.Windows.ShellExtension;

using WebDAVDrive.Dialogs;
using WinUIEx;


namespace WebDAVDrive.Services
{
    public class DrivesService : IDrivesService
    {
        /// <summary>
        /// Application settings.
        /// </summary>
        public AppSettings Settings { get; private set; }

        /// <summary>
        /// Provides sync root registration functionality and sparse package installation.
        /// </summary>
        public Registrar Registrar { get; private set; }

        /// <summary>
        /// Outputs logging information.
        /// </summary>
        public LogFormatter LogFormatter { get; private set; }

        /// <summary>
        /// Processes console input.
        /// </summary>
        public ConsoleProcessor ConsoleProcessor { get; private set; }

        /// <summary>
        /// Notification service.
        /// </summary>
        public IToastNotificationService NotificationService { get; private set; }

        /// <summary>
        /// Local com server.
        /// </summary>
        private LocalServer localServer;

        /// <summary>
        /// Tray icon when any drives are mounted.
        /// </summary>
        private DefaultTrayIcon? defaultTrayIcon;

        /// <summary>
        /// Secure storage service.
        /// </summary>
        private readonly SecureStorageService secureStorage;

        /// <summary>
        /// Engine instances. 
        /// Each item contains Engine instance ID and engine itself.
        /// Instance ID is used to delete the Engine from this list if file system is unmounted.
        /// </summary>
        public ConcurrentDictionary<Guid, VirtualEngine> Engines { get; } = new ConcurrentDictionary<Guid, VirtualEngine>();

        private readonly DrivesTrayIconService trayIconService;

        public DrivesService(AppSettings settings, LocalServer localServer, IToastNotificationService notificationService,
            SecureStorageService secureStorage, LogFormatter logFormatter)
        {
            this.localServer = localServer;
            this.secureStorage = secureStorage;

            Registrar = new Registrar(logFormatter.Log);
            Settings = settings;
            LogFormatter = logFormatter;
            trayIconService = new DrivesTrayIconService(this, logFormatter);
            NotificationService = notificationService;
            ConsoleProcessor = new ConsoleProcessor(Registrar, LogFormatter, settings.AppID);

        }

        public async Task<(bool success, Exception? exception)> MountNewAsync(string webDAVServerUrl)
        {
            // Register sync root and run User File System Engine.
            (bool success, Exception? exception) result = await TryMountNewAsync(webDAVServerUrl);

            if (result.success)
            {
                // Remove default tray icon.
                if (Engines.Count > 0 && defaultTrayIcon != null)
                {
                    defaultTrayIcon.Dispose();
                }
            }
            else
            {
                // Unmount engine if mounting failed.
                KeyValuePair<Guid, VirtualEngine>? engine = Engines.Where(p => p.Value.RemoteStorageRootPath == webDAVServerUrl).FirstOrDefault();
                if (engine != null)
                {
                    await UnMountAsync(engine.Value.Value.InstanceId, webDAVServerUrl);
                }
            }

            return result;
        }

        public async Task UnMountAsync(Guid engineId, string webDAVServerUrl)
        {
            await Registrar.UnregisterSyncRootAsync(Engines[engineId].Path, Engines[engineId].DataPath, LogFormatter.Log);

            // Remove engine from console processor.
            ConsoleProcessor.Commands.TryRemove(engineId, out _);

            if (Engines.TryRemove(engineId, out _))
            {
                trayIconService.RemoveTrayIcon(engineId);
            }

            if (Engines.Count == 0)
            {
                // Create main tray icon for app. 
                CreateDefaultTrayIcon();
            }
        }

        public async Task EnginesExitAsync()
        {
            foreach (KeyValuePair<Guid, VirtualEngine> engine in Engines)
            {
                await (engine.Value as VirtualEngine)!.Commands.EngineExitAsync();
                engine.Value.Dispose();

                trayIconService.RemoveTrayIcon(engine.Key);
            }
        }

        public async Task InitializeAsync(bool displayMountNewDriveWindow)
        {
            try
            {
                ValidatePackagePrerequisites();

                // Read mounted file systems.
                IEnumerable<StorageProviderSyncRootInfo> syncRoots = await Registrar.GetMountedSyncRootsAsync(Settings.AppID, LogFormatter.Log);
                if (syncRoots.Any())
                {
                    // This is an app restart or machine reboot. Roots were already mounted during previous runs. 
                    await RunExistingAsync(syncRoots);
                }
                else if (Settings.WebDAVServerURLs.Length != 0)
                {
                    // This is the first run of the app. Mount new drives.
                    foreach (string webDAVServerUrl in Settings.WebDAVServerURLs)
                    {
                        await MountNewAsync(webDAVServerUrl);
                    }
                }
                else
                {
                    // Create main tray icon for app. 
                    CreateDefaultTrayIcon();

                    if (displayMountNewDriveWindow)
                    {
                        // Get system uptime in milliseconds.
                        TimeSpan systemUptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                        // Define a threshold (e.g., 5 minutes) to determine if app was started after boot.
                        TimeSpan startupThreshold = TimeSpan.FromMinutes(5);
                        bool startedByStartup = systemUptime < startupThreshold;

                        if (!startedByStartup && Windows.Storage.ApplicationData.Current.LocalSettings.Values.ContainsKey("StartupWindowDoNotShowAgain"))
                        {
                            // Show mount new drive window.
                            _ = ServiceProvider.DispatcherQueue.TryEnqueue(() => new MountNewDrive().Show());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogFormatter.Log.Error($"\n\n Please fully uninstall the app. Then start the app again.\n\n", ex);
            }
        }

        /// <summary>
        /// Ensures that a <see cref="VirtualEngine"/> instance is mounted and ready for the given mount URL.
        /// </summary>
        /// <param name="mountUrl">
        /// The <see cref="Uri"/> representing the remote storage URL to check or mount.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> that represents the asynchronous operation, returning an instance of 
        /// <see cref="VirtualEngine"/> if successful, or <c>null</c> if the engine could not be mounted.
        /// </returns>
        /// <remarks>
        /// This method performs the following steps:
        /// 1. Checks if an engine with the specified <paramref name="mountUrl"/> is already mounted.
        ///    - If found and its state is <see cref="EngineState.Stopped"/>, it attempts to start the engine.
        /// 2. If no existing engine is found, it mounts a new engine for the provided URL.
        /// 3. After mounting, it retrieves and returns the newly mounted engine if available.
        /// 
        /// The method ensures that the specified remote storage is accessible by either starting an existing 
        /// engine or creating a new one if necessary.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="mountUrl"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if mounting a new engine fails.
        /// </exception>
        public async Task<VirtualEngine?> EnsureEngineMountedAsync(Uri mountUrl)
        {
            VirtualEngine? engine = Engines
             .FirstOrDefault(p => p.Value.RemoteStorageRootPath.Trim('/') == mountUrl.AbsoluteUri.Trim('/')).Value;

            // If engine exists and is stopped, start it
            if (engine?.State == EngineState.Stopped)
            {
                await engine.StartAsync();
            }

            if (engine != null)
                return engine;

            // If engine not found, mount a new one
            await MountNewAsync(mountUrl.AbsoluteUri);

            // Retrieve the newly mounted engine
            engine = Engines
                .FirstOrDefault(p => p.Value.RemoteStorageRootPath == mountUrl.AbsoluteUri).Value;

            return engine;
        }

        /// <summary>
        /// Converts the existing dictionary of engines to a new dictionary 
        /// with keys of type <see cref="Guid"/> and values of type <see cref="EngineWindows"/>.
        /// </summary>
        /// <exception cref="InvalidCastException">
        /// Thrown if any value in the original dictionary cannot be cast to <see cref="EngineWindows"/>.
        /// </exception>
        public ConcurrentDictionary<Guid, EngineWindows> GetEngineWindowsDictionary()
        {
            return new ConcurrentDictionary<Guid, EngineWindows>(
                Engines.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (EngineWindows)kvp.Value
                ));
        }


        /// <summary>
        /// Check if there is any remains from previous program deployments.
        /// </summary>
        private void ValidatePackagePrerequisites()
        {
            if (PackageRegistrar.IsRunningWithIdentity())
            {
                PackageRegistrar.EnsureIdentityContextIsCorrect();
                PackageRegistrar.EnsureNoConflictingClassesRegistered();
            }
        }

        private async Task RunExistingAsync(IEnumerable<StorageProviderSyncRootInfo> syncRoots)
        {
            List<Task> tasks = new List<Task>();
            foreach (StorageProviderSyncRootInfo syncRoot in syncRoots)
            {
                // Run each engine in a separate thread, to avoid blocking, if login UI is displayed.
                tasks.Add(Task.Run(async () =>
                {
                    string webDAVServerUrl = syncRoot.GetRemoteStoragePath();

                    // Run the User File System Engine.
                    await TryCreateEngineAsync(webDAVServerUrl, syncRoot.Path.Path);
                }));
            }

            Task.WaitAll(tasks.ToArray());
        }

        private async Task<(bool success, Exception? exception)> TryMountNewAsync(string webDAVServerUrl)
        {
            string? userFileSystemRootPath = null;
            try
            {
                userFileSystemRootPath = GenerateRootPathForProtocolMounting();
                string displayName = GetDisplayName(webDAVServerUrl);

                // Register sync root and create app folders.
                await Registrar.RegisterSyncRootAsync(
                    GetSyncRootId(webDAVServerUrl),
                    userFileSystemRootPath,
                    webDAVServerUrl,
                    displayName,
                    Path.Combine(Settings.IconsFolderPath, "Drive.ico"));
            }
            catch (Exception ex)
            {
                LogFormatter.Log.Error($"Failed to mount file system {webDAVServerUrl} {userFileSystemRootPath}", ex);
                return (false, ex);
            }
            // Run the User File System Engine.
            return await TryCreateEngineAsync(webDAVServerUrl, userFileSystemRootPath);
        }

        private async Task<(bool success, Exception? exception)> TryCreateEngineAsync(string webDAVServerUrl, string userFileSystemRootPath)
        {
            try
            {
                Uri webDAVServer = new Uri(webDAVServerUrl);
                string webSocketsProtocol = webDAVServer.Scheme == "https" ? "wss" : "ws";
                string webSocketServerUrl = $"{webSocketsProtocol}://{webDAVServer.Authority}/";

                VirtualEngine engine = new VirtualEngine(
                    userFileSystemRootPath,
                    webDAVServerUrl,
                    webSocketServerUrl,
                    secureStorage,
                    this,
                    LogFormatter,
                    Settings);

                Engines.TryAdd(engine.InstanceId, engine);
                ConsoleProcessor.Commands.TryAdd(engine.InstanceId, engine.Commands);

                engine.SyncService.SyncIntervalMs = Settings.SyncIntervalMs;
                engine.SyncService.IncomingSyncMode = VirtualEngine.GetSyncMode(Settings.IncomingSyncMode);
                engine.AutoLock = Settings.AutoLock;
                engine.MaxTransferConcurrentRequests = Settings.MaxTransferConcurrentRequests.Value;
                engine.MaxOperationsConcurrentRequests = Settings.MaxOperationsConcurrentRequests.Value;
                engine.FolderInvalidationIntervalMs = Settings.FolderInvalidationIntervalMs;

                // Print Engine config, settings, logging headers.
                await LogFormatter.PrintEngineStartInfoAsync(engine, webDAVServerUrl);

                // Create tray.
                trayIconService.CreateTrayIcon(GetDisplayName(webDAVServerUrl), engine.InstanceId, engine);

                // Start processing OS file system calls.
                await engine.StartAsync();

                return (true, null);
            }
            catch (InvalidLicenseException ex) // Check if it is license validation error.
            {
                LogFormatter.Log.Error($"License validation failed", ex);
                NotificationService.ShowLicenseError(ex);

                return (false, ex);
            }
            catch (Exception ex)
            {
                LogFormatter.Log.Error($"Failed to start Engine {webDAVServerUrl} {userFileSystemRootPath}", ex);

                return (false, ex);
            }
        }

        private string GetDisplayName(string webDAVServerUrl)
        {
            return webDAVServerUrl.Replace("http://", "").Replace("https://", "").TrimEnd('/');
        }

        private string GenerateRootPathForProtocolMounting()
        {
            string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string randomName = Path.GetRandomFileName();
            string folderName = Path.GetFileNameWithoutExtension(randomName);
            return Path.Combine(userProfilePath, "DAV", folderName);
        }

        /// <summary>
        /// Gets automatically generated Sync Root ID.
        /// </summary>
        /// <remarks>An identifier in the form: [Storage Provider ID]![Windows SID]![Account ID]</remarks>
        private string GetSyncRootId(string remoteStoragePathRoot)
        {
            return $"{Settings.AppID}!{System.Security.Principal.WindowsIdentity.GetCurrent().User}!{remoteStoragePathRoot}";
        }

        /// <summary>
        /// Creates default tray icon with "Mount new Drive" menu.
        /// </summary>
        private void CreateDefaultTrayIcon()
        {
            defaultTrayIcon = new DefaultTrayIcon(this);
            defaultTrayIcon.CreateTrayIcon();
        }
    }
}
