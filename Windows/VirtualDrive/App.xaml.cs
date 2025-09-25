using log4net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VirtualDrive.Dialogs;
using Windows.ApplicationModel.Resources;
using WinUIEx;
using WindowManager = ITHit.FileSystem.Samples.Common.Windows.WindowManager;

using ITHit.FileSystem.Extensions;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Windows.Package;
using ITHit.FileSystem.Windows.WinUI;
using ITHit.FileSystem.Windows.WinUI.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualDrive
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
#if DEBUG
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeConsole();
#endif

        public IServiceProvider Services { get; private set; }
        public IConfiguration Configuration { get; private set; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            // Load Settings.
            Configuration = ConfigureAppConfiguration();
            Services = ConfigureServices(Configuration);

            // Set the service provider.
            ServiceProvider.Services = Services;
            ServiceProvider.DispatcherQueue = DispatcherQueue.GetForCurrentThread();
            
#if DEBUG
            // Display Console.
            AllocConsole();
            WindowManager.ConfigureConsole();
#endif
            LogFormatter logFormatter = ServiceProvider.GetService<LogFormatter>();
            // Log environment description.
            logFormatter.PrintEnvironmentDescription();

            InitializeComponent();
        }

        private IConfiguration ConfigureAppConfiguration()
        {
            return new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", false, true)
            .Build();
        }

        private static IServiceProvider ConfigureServices(IConfiguration configuration)
        {
            ServiceCollection serviceCollection = new ServiceCollection();

            // Register your services here
            serviceCollection.AddSingleton(options => LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType));
            serviceCollection.AddSingleton(options => configuration.ReadSettings());
            serviceCollection.AddSingleton(options => new LogFormatter(ServiceProvider.GetService<ILog>(), ServiceProvider.GetService<AppSettings>().AppID));
            serviceCollection.AddSingleton(options => new Registrar(ServiceProvider.GetService<ILog>()));
            serviceCollection.AddSingleton(options => new ConsoleProcessor(ServiceProvider.GetService<Registrar>(), ServiceProvider.GetService<LogFormatter>(),
                ServiceProvider.GetService<AppSettings>().AppID));
            serviceCollection.AddSingleton(options => {
                AppSettings settings = ServiceProvider.GetService<AppSettings>();
                return new VirtualEngine(
                    settings.UserFileSystemLicense,
                    settings.UserFileSystemRootPath,
                    settings.RemoteStorageRootPath,
                    settings.IconsFolderPath,
                    ServiceProvider.GetService<LogFormatter>()); });
            serviceCollection.AddSingleton(options => new Commands(ServiceProvider.GetService<VirtualEngine>(),
                ServiceProvider.GetService<AppSettings>().RemoteStorageRootPath, ServiceProvider.GetService<ILog>()));

            return serviceCollection.BuildServiceProvider();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    ValidatePackagePrerequisites();

                    // Register this app to process COM shell extensions calls.
                    using (ShellExtension.ShellExtensions.StartComServer())
                    {
                        // Run the User File System Engine.
                        await RunEngineAsync();
                    }
                }
                catch (Exception ex)
                {
                    ServiceProvider.GetService<ILog>().Error($"\n\n Press Shift-Esc to fully uninstall the app. Then start the app again.\n\n", ex);
                    await ServiceProvider.GetService<ConsoleProcessor>().ProcessUserInputAsync();
                }
            });

            mainWindow = new MainWindow();
            mainWindow.Activate();
        }

        private Window? mainWindow;

        /// <summary>
        /// Check if there is any remains from previous program deployments.
        /// </summary>
        private static void ValidatePackagePrerequisites()
        {
            PackageRegistrar.EnsureIdentityContextIsCorrect();
            PackageRegistrar.EnsureNoConflictingClassesRegistered();
        }

        private async Task RunEngineAsync()
        {
            AppSettings settings = ServiceProvider.GetService<AppSettings>();
            LogFormatter logFormatter = ServiceProvider.GetService<LogFormatter>();
            Registrar registrar = ServiceProvider.GetService<Registrar>();
            ConsoleProcessor consoleProcessor = ServiceProvider.GetService<ConsoleProcessor>();
 
            // Register sync root and create app folders.
            await registrar.RegisterSyncRootAsync(
                SyncRootId,
                settings.UserFileSystemRootPath,
                settings.RemoteStorageRootPath,
                settings.ProductName,
                Path.Combine(settings.IconsFolderPath, "Drive.ico"), settings.CustomColumns);

            using (VirtualEngine engine = ServiceProvider.GetService<VirtualEngine>())
            {
                Commands commands = ServiceProvider.GetService<Commands>();
                commands.RemoteStorageMonitor = engine.RemoteStorageMonitor;
                consoleProcessor.Commands.TryAdd(Guid.Empty, commands);

                // Here we disable incoming sync. To get changes using pooling call IncomingPooling.ProcessAsync()
                engine.SyncService.IncomingSyncMode = ITHit.FileSystem.Synchronization.IncomingSyncMode.Disabled;

                engine.SyncService.SyncIntervalMs = settings.SyncIntervalMs;
                engine.AutoLock = settings.AutoLock;
                engine.MaxTransferConcurrentRequests = settings.MaxTransferConcurrentRequests.Value;
                engine.MaxOperationsConcurrentRequests = settings.MaxOperationsConcurrentRequests.Value;

                // Set the remote storage item ID for the root item. It will be passed to the IEngine.GetFileSystemItemAsync()
                // method as a remoteStorageItemId parameter when a root folder is requested.
                byte[] itemId = WindowsFileSystemItem.GetItemIdByPath(settings.RemoteStorageRootPath);
                engine.SetRemoteStorageRootItemId(itemId);

                // Print console commands.
                consoleProcessor.PrintHelp();

                // Print Engine config, settings, logging headers.
                await logFormatter.PrintEngineStartInfoAsync(engine, settings.RemoteStorageRootPath);

                // Start processing OS file system calls.
                await engine.StartAsync();

                // Sync all changes from remote storage one time for demo purposes.
                await engine.SyncService.IncomingPooling.ProcessAsync();

                CreateTrayIcon();
#if DEBUG
                // Opens Windows File Manager with user file system folder and remote storage folder.
                commands.ShowTestEnvironment(settings.ProductName);
#endif
                // Keep this application running and reading user input.
                await consoleProcessor.ProcessUserInputAsync(() =>
                {
                    ServiceProvider.DispatcherQueue.TryEnqueue(() => Current.Exit());
                });
            }
        }


        /// <summary>
        /// Gets automatically generated Sync Root ID.
        /// </summary>
        /// <remarks>An identifier in the form: [Storage Provider ID]![Windows SID]![Account ID]</remarks>
        private string SyncRootId
        {
            get
            {
                return $"{ServiceProvider.GetService<AppSettings>().AppID}!{System.Security.Principal.WindowsIdentity.GetCurrent().User}!User";
            }
        }

        private void CreateTrayIcon()
        {
            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            AppSettings settings = ServiceProvider.GetService<AppSettings>();
            LogFormatter logFormatter = ServiceProvider.GetService<LogFormatter>();
            VirtualEngine engine = ServiceProvider.GetService<VirtualEngine>();

            ServiceProvider.DispatcherQueue.TryEnqueue(() =>
            {
                Tray trayWindow = new Tray(engine, null, null);
                Commands commands = new Commands(engine, settings.RemoteStorageRootPath, logFormatter.Log);

                //Set header text, mount, unmount, start/stop sync handlers here - as Tray does not access to sample related things
                trayWindow.DriveNameText = settings.RemoteStorageRootPath.EllipsisAtStart(40, '\\');
                trayWindow.NotifyIconText = $"{settings.ProductName}\n{settings.RemoteStorageRootPath}";
                trayWindow.Header = settings.ProductName;
                trayWindow.ShowSettingsMenu.Visibility = Visibility.Collapsed;
                trayWindow.MountNewDriveMenu.Visibility = Visibility.Collapsed;
                trayWindow.UnmountMenu.Visibility = Visibility.Collapsed;
                trayWindow.ShowFeedbackMenu.Click += async (sender, e) => await Commands.OpenSupportPortalAsync();
                trayWindow.OpenFolderButton.Click += (sender, e) => commands.TryOpen(engine.Path);
                trayWindow.ViewOnlineMenusVisible = false;
                trayWindow.ErrorDescriptionClick += (viewModel) => ErrorDescriptionClick(viewModel, engine);

#if DEBUG
                MenuFlyoutItem hideShowConsole = new MenuFlyoutItem { Text = resourceLoader.GetString("HideLog") };
                hideShowConsole.Click += (_, _) => HideShowConsoleClicked(hideShowConsole);
                trayWindow.DebugMenu.Items.Add(hideShowConsole);
#endif
                MenuFlyoutItem enableDisableDebugLoggingMenu = new MenuFlyoutItem
                {
                    Text = logFormatter.DebugLoggingEnabled ? resourceLoader.GetString("DisableDebugLogging")
                    : resourceLoader.GetString("EnableDebugLogging")
                };
                enableDisableDebugLoggingMenu.Click += (sender, e) => EnableDisableDebugLoggingClicked(enableDisableDebugLoggingMenu);
                MenuFlyoutItem openLogFile = new MenuFlyoutItem { Text = resourceLoader.GetString("OpenLogFile/Text") };
                openLogFile.Click += (_, _) => Commands.TryOpen(logFormatter.LogFilePath);

                trayWindow.DebugMenu.Items.Add(enableDisableDebugLoggingMenu);
                trayWindow.DebugMenu.Items.Add(openLogFile);
            });
        }

        /// <summary>
        /// Enables/Disables debug logging.
        /// </summary>
        private void EnableDisableDebugLoggingClicked(MenuFlyoutItem menuItem)
        {
            LogFormatter logFormatter = ServiceProvider.GetService<LogFormatter>();
            logFormatter.DebugLoggingEnabled = !logFormatter.DebugLoggingEnabled;
            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            menuItem.Text = resourceLoader.GetString(logFormatter.DebugLoggingEnabled ? "DisableDebugLogging" : "EnableDebugLogging");
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

        private void ErrorDescriptionClick(FileEventViewModel fileEvent, VirtualEngine engine)
        {
            new ErrorDetails(fileEvent, engine, ServiceProvider.GetService<LogFormatter>(), ServiceProvider.GetService<AppSettings>()).Show();
        }
    }
}
