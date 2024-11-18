using System.Collections.Concurrent;

using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Windows.ShellExtension;
using ITHit.FileSystem.Windows.Package;
using ITHit.FileSystem;

using WebDAVDrive.Platforms.Windows.Services;
using WebDAVDrive.Platforms.Windows.Utils;

namespace WebDAVDrive.Services
{
    public class DomainsService : IDomainsService
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
        private AppTrayIconService? appTrayIconService;

        /// <summary>
        /// Engine instances. 
        /// Each item contains Engine instance ID and engine itself.
        /// Instance ID is used to delete the Engine from this list if file system is unmounted.
        /// </summary>
        public ConcurrentDictionary<Guid, VirtualEngine> Engines { get; } = new ConcurrentDictionary<Guid, VirtualEngine>();

        private readonly DriveTrayIconService trayIconService;  

        public DomainsService(AppSettings settings, LocalServer localServer, IToastNotificationService notificationService, LogFormatter logFormatter)
        {
            this.Registrar = new Registrar(logFormatter.Log, ShellExtension.ShellExtensions.Handlers);
            this.Settings = settings;
            this.LogFormatter = logFormatter;
            this.localServer = localServer;
            this.trayIconService = new DriveTrayIconService(this, logFormatter);
            this.NotificationService = notificationService;
            this.ConsoleProcessor = new ConsoleProcessor(this.Registrar, this.LogFormatter, settings.AppID);
        }

        public async Task MountNewAsync(string[] webDAVServerURLs)
        {
            // Mount new file system for each URL, run Engine and tray app.
            foreach (string webDAVServerUrl in webDAVServerURLs)
            {
                // Register sync root and run User File System Engine.
                await TryMountNewAsync(webDAVServerUrl);
            }

            // Remove main tray icon.
            if (Engines.Count > 0 && appTrayIconService != null)
            {
                appTrayIconService.Dispose();
            }
        }

        public async Task UnMountAsync(Guid engineId, string webDAVServerUrl)
        {
            await Registrar.UnregisterSyncRootAsync(Engines[engineId].Path, Engines[engineId].DataPath, LogFormatter.Log);

            // Remove engine from console processor.
            ConsoleProcessor.Commands.TryRemove(engineId, out _);

            if (Engines.TryRemove(engineId, out _))
            {
                trayIconService.DisposeTrayIcon(engineId);
            }

            if (Engines.Count == 0)
            {
                // Create main tray icon for app. 
                CreateMainTrayIcon();
            }
        }

        public async Task EnginesExitAsync()
        {
            foreach (var engine in Engines)
            {
                await (engine.Value as VirtualEngine)!.Commands.EngineExitAsync();
                engine.Value.Dispose();

                trayIconService.DisposeTrayIcon(engine.Key);
            }
        }

        public async Task InitializeAsync(bool displayMountNewDriveWindow)
        {
            try
            {
                ValidatePackagePrerequisites();

                // Read mounted file systems.
                var syncRoots = await Registrar.GetMountedSyncRootsAsync(Settings.AppID, LogFormatter.Log);
                if (syncRoots.Any())
                {
                    // This is an app restart or machine reboot. Roots were already mounted during previous runs. 
                    await RunExistingAsync(syncRoots);
                }
                else if (Settings.WebDAVServerURLs.Length != 0)
                {
                    await MountNewAsync(Settings.WebDAVServerURLs);
                }
                else
                {
                    // Create main tray icon for app. 
                    CreateMainTrayIcon();

                    if (displayMountNewDriveWindow)
                    {
                        // Get system uptime in milliseconds.
                        TimeSpan systemUptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                        // Define a threshold (e.g., 5 minutes) to determine if app was started after boot.
                        TimeSpan startupThreshold = TimeSpan.FromMinutes(5);
                        bool startedByStartup = systemUptime < startupThreshold;

                        if (!startedByStartup && Preferences.Get("StartupWindowDoNotShowAgain", false))
                        {
                            // Show mount new drive window.
                            _ = MainThread.InvokeOnMainThreadAsync(DialogsUtil.OpenMountNewDriveWindow);
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
             .FirstOrDefault(p => p.Value.RemoteStorageRootPath == mountUrl.AbsoluteUri).Value;

            // If engine exists and is stopped, start it
            if (engine?.State == EngineState.Stopped)
            {
                await engine.StartAsync();
            }

            if (engine != null)
                return engine;

            // If engine not found, mount a new one
            await MountNewAsync(new[] { mountUrl.AbsoluteUri });

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

        private async Task RunExistingAsync(IEnumerable<Windows.Storage.Provider.StorageProviderSyncRootInfo> syncRoots)
        {
            List<Task> tasks = new List<Task>();
            foreach (var syncRoot in syncRoots)
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

        private async Task<bool> TryMountNewAsync(string webDAVServerUrl)
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
                return false;
            }
            // Run the User File System Engine.
            return await TryCreateEngineAsync(webDAVServerUrl, userFileSystemRootPath);
        }

        private async Task<bool> TryCreateEngineAsync(string webDAVServerUrl, string userFileSystemRootPath)
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
               
                // Print Engine config, settings, logging headers.
                await LogFormatter.PrintEngineStartInfoAsync(engine, webDAVServerUrl);

                // Create tray.
                trayIconService.CreateTrayIcon(GetDisplayName(webDAVServerUrl), engine.InstanceId, engine);

                // Start processing OS file system calls.
                await engine.StartAsync();

                return true;
            }
            catch (InvalidLicenseException ex) // Check if it is license validation error.
            {
                LogFormatter.Log.Error($"License validation failed", ex);
                NotificationService.ShowLicenseErrorToast(ex);

                return false;
            }
            catch (Exception ex)
            {
                LogFormatter.Log.Error($"Failed to start Engine {webDAVServerUrl} {userFileSystemRootPath}", ex);       

                return false;
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
        /// Creates main tray icon.
        /// </summary>
        private void CreateMainTrayIcon()
        {
            appTrayIconService = new AppTrayIconService(this);
            appTrayIconService.CreateTrayIcon();
        }
    }
}
