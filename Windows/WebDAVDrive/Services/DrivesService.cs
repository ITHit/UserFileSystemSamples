using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage.Provider;
using Windows.ApplicationModel.Resources;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WebDAVDrive.Dialogs;
using WebDAVDrive.Enums;
using WinUIEx;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Windows.Package;
using ITHit.FileSystem.Windows.ShellExtension;
using ITHit.FileSystem.Windows.WinUI.ViewModels;
using ITHit.FileSystem.Windows.WinUI;
using WindowManager = ITHit.FileSystem.Samples.Common.Windows.WindowManager;


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
        /// Secure storage service.
        /// </summary>
        private readonly SecureStorageService secureStorage;

        /// <summary>
        /// Engine instances. 
        /// Each item contains Engine instance ID and engine itself.
        /// Instance ID is used to delete the Engine from this list if file system is unmounted.
        /// </summary>
        public ConcurrentDictionary<Guid, VirtualEngine> Engines { get; } = new ConcurrentDictionary<Guid, VirtualEngine>();

        private readonly Dictionary<Guid, Tray> trayWindows;

        private Tray? defaultTrayWindow;

        public DrivesService(AppSettings settings, LocalServer localServer, IToastNotificationService notificationService,
            SecureStorageService secureStorage, LogFormatter logFormatter)
        {
            this.localServer = localServer;
            this.secureStorage = secureStorage;

            Registrar = new Registrar(logFormatter.Log);
            Settings = settings;
            LogFormatter = logFormatter;
            NotificationService = notificationService;
            ConsoleProcessor = new ConsoleProcessor(Registrar, LogFormatter, settings.AppID);
            trayWindows = new Dictionary<Guid, Tray>();
        }

        public async Task<(bool success, Exception? exception)> MountNewAsync(string webDAVServerUrl)
        {
            // Register sync root and run User File System Engine.
            (bool success, Exception? exception) result = await TryMountNewAsync(webDAVServerUrl);

            return result;
        }

        public async Task UnMountAsync(Guid? engineGuid)
        {
            if (engineGuid == null) return;
            Guid engineId = engineGuid.Value;
            await Registrar.UnregisterSyncRootAsync(Engines[engineId].Path, Engines[engineId].DataPath, LogFormatter.Log);

            // Remove engine from console processor.
            ConsoleProcessor.Commands.TryRemove(engineId, out _);

            if (Engines.TryRemove(engineId, out _))
            {
                if (Engines.Count > 0)
                {
                    RemoveTrayWindow(engineId);
                }
                //we unmounted last existing engine - so call SetEngineAsync on exisyting Tray window
                else
                {
                    ServiceProvider.DispatcherQueue.TryEnqueue(async () =>
                    {
                        Tray existingTrayWindow = trayWindows[engineId];
                        await trayWindows[engineId].SetEngineAsync(null, null, null);
                        AdjustEngineRelatedThings(existingTrayWindow, null);
                        //as single window is not more connected to engine - add it to dictionary with Guid.Empty
                        trayWindows.Remove(engineId);
                        defaultTrayWindow = existingTrayWindow;
                    });
                }
            }
        }

        public async Task EnginesExitAsync()
        {
            foreach (KeyValuePair<Guid, VirtualEngine> engine in Engines)
            {
                await engine.Value!.Commands.EngineExitAsync();
                engine.Value.Dispose();

                RemoveTrayWindow(engine.Key);
            }
            //Remove Tray instance without engine - in case it exists
            RemoveTrayWindow(Guid.Empty);
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
                    CreateTrayIcon(null);

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
            PackageRegistrar.EnsureIdentityContextIsCorrect();
            PackageRegistrar.EnsureNoConflictingClassesRegistered();
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
                    Path.Combine(Settings.IconsFolderPath, "Drive.ico"),
                    Settings.CustomColumns);
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
                engine.MaxTransferConcurrentRequests = Settings.MaxTransferConcurrentRequests.Value;
                engine.MaxOperationsConcurrentRequests = Settings.MaxOperationsConcurrentRequests.Value;
                engine.FolderInvalidationIntervalMs = Settings.FolderInvalidationIntervalMs;

                // Print Engine config, settings, logging headers.
                await LogFormatter.PrintEngineStartInfoAsync(engine, webDAVServerUrl);

                if (defaultTrayWindow != null)
                {
                    await defaultTrayWindow.SetEngineAsync(engine, engine?.Settings?.Compare, engine?.Settings?.TrayMaxHistoryItems);
                    AdjustEngineRelatedThings(defaultTrayWindow, engine);
                    //remap single window to new engine in dictionary and clear defaultTrayWindow variable
                    trayWindows.Add(engine!.InstanceId, defaultTrayWindow);
                    defaultTrayWindow = null;
                }
                // Create tray window.
                else
                {
                    CreateTrayIcon(engine);
                }

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

        private void CreateTrayIcon(VirtualEngine? engine)
        {
            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            ServiceProvider.DispatcherQueue.TryEnqueue(() =>
            {
                
                // Create Tray window.
                Tray trayWindow = new Tray(engine, engine?.Settings?.Compare, engine?.TrayMaxHistoryItems);

                //Set header text, mount, unmount, start/stop sync handlers here - as Tray does not access to sample related things                    
                trayWindow.Header = ServiceProvider.GetService<AppSettings>().ProductName;
                trayWindow.ShowSettingsMenu.Click += (sender, e) => ShowSettingsMenuClick(trayWindow);
                trayWindow.MountNewDriveMenu.Click += (sender, e) => TrayMountNewDriveClick();
                trayWindow.UnmountMenu.Click += (sender, e) => TrayUnmountClick(trayWindow.Engine as VirtualEngine);
                trayWindow.ShowFeedbackMenu.Click += async (sender, e) => await Commands.OpenSupportPortalAsync();
                trayWindow.OpenFolderButton.Click += (sender, e) => (trayWindow.Engine as VirtualEngine)?.Commands?.TryOpen(trayWindow.Engine?.Path ?? string.Empty);
                trayWindow.ViewOnlineButton.Click += (sender, e) => ViewOnlineClicked(trayWindow.Engine as VirtualEngine);
                trayWindow.ViewItemOnlineClick += (viewModel) => ViewItemOnlineClick(viewModel, trayWindow.Engine as VirtualEngine);
                trayWindow.ErrorDescriptionClick += (viewModel) => ErrorDescriptionClick(viewModel, trayWindow.Engine as VirtualEngine, LogFormatter);
                trayWindow.LoginMenuItem.Click += (sender, e) => LoginMenuItemClick(trayWindow.Engine as VirtualEngine);
                trayWindow.LogoutMenuItem.Click += (sender, e) => LogoutMenuItemClick(trayWindow.Engine as VirtualEngine);
#if DEBUG
                MenuFlyoutItem hideShowConsole = new MenuFlyoutItem { Text = resourceLoader.GetString("HideLog") };
                hideShowConsole.Click += (_, _) => HideShowConsoleClicked(hideShowConsole);
                trayWindow.DebugMenu.Items.Add(hideShowConsole);
#endif

                MenuFlyoutItem enableDisableDebugLoggingMenu = new MenuFlyoutItem
                {
                    Text = LogFormatter.DebugLoggingEnabled ? resourceLoader.GetString("DisableDebugLogging")
                    : resourceLoader.GetString("EnableDebugLogging")
                };
                enableDisableDebugLoggingMenu.Click += (sender, e) => EnableDisableDebugLoggingClicked(enableDisableDebugLoggingMenu);
                MenuFlyoutItem openLogFile = new MenuFlyoutItem { Text = resourceLoader.GetString("OpenLogFile/Text") };
                openLogFile.Click += (_, _) => Commands.TryOpen(LogFormatter.LogFilePath);

                trayWindow.DebugMenu.Items.Add(enableDisableDebugLoggingMenu);
                trayWindow.DebugMenu.Items.Add(openLogFile);

                //Assing events to context menu strip (for mode without engine)
                trayWindow.RequestSupportContextMenu.Click += async (sender, e) => await Commands.OpenSupportPortalAsync();
                trayWindow.MountNewDriveContextMenu.Click += (sender, e) => TrayMountNewDriveClick();
                trayWindow.HideShowLogContextMenu.Click += (_, _) =>
                {
                    WindowManager.SetConsoleWindowVisibility(!WindowManager.ConsoleVisible);
                    trayWindow.HideShowLogContextMenu.Text = WindowManager.ConsoleVisible ? resourceLoader.GetString("HideLog") : resourceLoader.GetString("ShowLog");
                };
                trayWindow.EnableDisableDebugLoggingContextMenu.Text = LogFormatter.DebugLoggingEnabled ?
                    resourceLoader.GetString("DisableDebugLogging") : resourceLoader.GetString("EnableDebugLogging");
                trayWindow.EnableDisableDebugLoggingContextMenu.Click += (_, _) =>
                {
                    LogFormatter.DebugLoggingEnabled = !LogFormatter.DebugLoggingEnabled;
                    trayWindow.EnableDisableDebugLoggingContextMenu.Text = LogFormatter.DebugLoggingEnabled ?
                        resourceLoader.GetString("DisableDebugLogging") : resourceLoader.GetString("EnableDebugLogging");
                };
                trayWindow.OpenLogFileContextMenu.Click += (_, _) =>
                {
                    if (LogFormatter?.LogFilePath != null && File.Exists(LogFormatter?.LogFilePath))
                    {
                        Commands.TryOpen(LogFormatter.LogFilePath);
                    }
                };

                AdjustEngineRelatedThings(trayWindow, engine);
                
                //if engine is null - set defaultTrayWindow, otherwise add to dictionary
                if (engine == null) defaultTrayWindow = trayWindow;
                else trayWindows.Add(engine.InstanceId, trayWindow);
            });
        }

        private void AdjustEngineRelatedThings(Tray trayWindow, VirtualEngine? engine)
        {
            string productName = ServiceProvider.GetService<AppSettings>().ProductName;
            ServiceProvider.DispatcherQueue.TryEnqueue(() =>
            {
                trayWindow.DriveNameText = engine?.RemoteStorageRootPath ?? string.Empty;
                trayWindow.NotifyIconText = $"{productName}{(engine != null ? "\n" + engine.RemoteStorageRootPath : string.Empty)}";

                if (engine != null)
                {
                    engine.LoginStatusChanged += (_, e) =>
                    {
                        ServiceProvider.DispatcherQueue.TryEnqueue(() =>
                        {
                            trayWindow.LoginMenuItem.Visibility = e == EngineAuthentificationStatus.LoggedOut ? Visibility.Visible : Visibility.Collapsed;
                            trayWindow.LogoutMenuItem.Visibility = e == EngineAuthentificationStatus.LoggedIn ? Visibility.Visible : Visibility.Collapsed;
                        });
                    };
                }
            });
        }

        private void ShowSettingsMenuClick(Tray trayWindow)
        {
            ServiceProvider.DispatcherQueue.TryEnqueue(() => _ = trayWindow.Engine is VirtualEngine ? new Settings(trayWindow).Show() : false);
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
            string accountId = WindowsIdentity.GetCurrent()?.User?.ToString() ?? "default";
            string contextId = GetFullSha1Hash($"{Sanitize(accountId)}|{Sanitize(remoteStoragePathRoot)}");
            string syncRootId = $"{Settings.AppID}!{contextId}";

            // Ensure it doesn't exceed Windows path limits
            if (syncRootId.Length > 200)
                syncRootId = syncRootId.Substring(0, 200);

            return syncRootId;
        }

        /// <summary>
        /// Replace invalid characters with underscore
        /// </summary>
        private static string Sanitize(string input)
        {
            return Regex.Replace(input, @"[^A-Za-z0-9._-]", "_");
        }

        /// <summary>
        /// Generates SHA1 hash for the input string.
        /// </summary>
        private static string GetFullSha1Hash(string input)
        {
            using (var sha1 = SHA1.Create())
            {
                byte[] bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(bytes).Replace("-", ""); // 40 hex chars
            }
        }

        
        private void ViewItemOnlineClick(FileEventViewModel fileEvent, VirtualEngine? engine)
        {
            string url = engine?.Mapping?.MapPath(fileEvent.Path) ?? string.Empty;
            Commands.Open(url);
        }

        private void ErrorDescriptionClick(FileEventViewModel fileEvent, VirtualEngine? engine, LogFormatter logFormatter)
        {
            new ErrorDetails(fileEvent, engine, logFormatter).Show();
        }

        private async void TrayUnmountClick(VirtualEngine? engine)
        {
            engine?.Commands?.StopEngineAsync()?.Wait();
            await UnMountAsync(engine?.InstanceId);
        }

        private void TrayMountNewDriveClick()
        {
            new MountNewDrive().Show();
        }

        /// <summary>
        /// Enables/Disables debug logging.
        /// </summary>
        private void EnableDisableDebugLoggingClicked(MenuFlyoutItem menuItem)
        {
            LogFormatter.DebugLoggingEnabled = !LogFormatter.DebugLoggingEnabled;
            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            menuItem.Text = resourceLoader.GetString(LogFormatter.DebugLoggingEnabled ? "DisableDebugLogging" : "EnableDebugLogging");
        }

        /// <summary>
        /// Hides/Shows console log.
        /// </summary>
        private void HideShowConsoleClicked(MenuFlyoutItem menuItem)
        {
            WindowManager.SetConsoleWindowVisibility(!WindowManager.ConsoleVisible);
            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            menuItem.Text = WindowManager.ConsoleVisible ? resourceLoader.GetString("HideLog") : resourceLoader.GetString("ShowLog");
        }

        /// <summary>
        /// Opens remote URL of the drive (if supported). Otherwise that button is disabled.
        /// </summary>
        private void ViewOnlineClicked(VirtualEngine? engine)
        {
            Commands.Open(engine?.RemoteStorageRootPath ?? string.Empty);
        }

        // Must create new thread to avoid deadlock.
        private void LoginMenuItemClick(VirtualEngine? engine)
        {
            Task.Run(() => engine?.StartAsync()?.Wait());
        }

        private void LogoutMenuItemClick(VirtualEngine? engine)
        {
            Task.Run(() => engine?.LogoutAsync()?.Wait());
        }
        

        private void RemoveTrayWindow(Guid engineKey)
        {
            if (trayWindows.ContainsKey(engineKey))
            {
                ServiceProvider.DispatcherQueue.TryEnqueue(() =>
                {
                    trayWindows[engineKey].Dispose();
                    trayWindows.Remove(engineKey);
                });
            }
        }
    }
}
