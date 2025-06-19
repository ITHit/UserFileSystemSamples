using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using log4net;

using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows.ShellExtension;
using WindowManager = ITHit.FileSystem.Samples.Common.Windows.WindowManager;

using WebDAVDrive.Services;
using System.Runtime.InteropServices;
using WebDAVDrive.ShellExtensions;


namespace WebDAVDrive
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
        public Window? Window { get; private set; }

        /// <summary>
        /// Initializes the singleton application object. This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            AppInstance singleInstance = AppInstance.FindOrRegisterForKey("SingleInstanceApp");

            if (!singleInstance.IsCurrent)
            {
                // Redirect to the existing instance.
                AppInstance currentInstance = AppInstance.GetCurrent();
                AppActivationArguments args = currentInstance.GetActivatedEventArgs();

                // Redirect activation and close this instance
                singleInstance.RedirectActivationToAsync(args).GetAwaiter().GetResult();
                Process.GetCurrentProcess().Kill();
                return;
            }

            // Register for future activations.
            singleInstance.Activated += SingleInstance_Activated;

            Configuration = ConfigureAppConfiguration();
            Services = ConfigureServices(Configuration);

            // Set the service provider.
            ServiceProvider.Services = Services;
            ServiceProvider.DispatcherQueue = DispatcherQueue.GetForCurrentThread();
            //assing IsDarkTheme property on application start
            ServiceProvider.IsDarkTheme = Current.RequestedTheme == ApplicationTheme.Dark;

#if DEBUG
            // Display Console.
            AllocConsole();
            WindowManager.ConfigureConsole();
#endif

            // Print environment description.
            Services.GetService<LogFormatter>()!.PrintEnvironmentDescription();

            InitializeComponent();
        }

        private void SingleInstance_Activated(object? sender, AppActivationArguments e)
        {
            if (e.Kind == ExtendedActivationKind.Protocol)
            {
                ProtocolActivatedEventArgs? protocolArgs = e.Data as ProtocolActivatedEventArgs;
                if (protocolArgs != null)
                {
                    // Open item from protocol Uri.
                    Uri uri = protocolArgs.Uri;
                    _ = Task.Run(async () => await OpenItemFromProtocolUriAsync(uri));
                }
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            IDrivesService domainsService = ServiceProvider.GetService<IDrivesService>();
            AppActivationArguments launchedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

            // We want the application to continue processing events, while engines are mounted and starting.
            _ = Task.Run(async () =>
            {
                // Init engines.                        
                await domainsService.InitializeAsync((launchedArgs.Data as ProtocolActivatedEventArgs)?.Uri == null);

                // Open url if app was opened via protocol.
                await OpenItemFromProtocolUriAsync((launchedArgs.Data as ProtocolActivatedEventArgs)?.Uri);
#if DEBUG
                foreach (VirtualEngine engine in domainsService.Engines.Values)
                {
                    engine.Commands.TryOpen(engine.Path);
                    Commands.Open(engine.RemoteStorageRootPath); // Open remote storage.
                }
#endif
            });

            _ = Task.Run(async () =>
            {
                // Print console commands.
                domainsService.ConsoleProcessor.PrintHelp();

                // Read user input.
                await domainsService.ConsoleProcessor.ProcessUserInputAsync(() =>
                {
                    ServiceProvider.DispatcherQueue.TryEnqueue(() => Current.Exit());
                });
            });

            // Create a main window.
            Window = new MainWindow();
            Window.Closed += WindowClosed;

#if !DEBUG
            if (!Windows.Storage.ApplicationData.Current.LocalSettings.Values.ContainsKey("StartupWindowDoNotShowAgain"))
            {
                ServiceProvider.DispatcherQueue.TryEnqueue(() => new Startup());
            }
#endif
        }

        private void WindowClosed(object sender, WindowEventArgs args)
        {
            try
            {
                ServiceProvider.GetService<IDrivesService>().EnginesExitAsync().Wait();         
                // Explicitly call Dispose for LocalServer.
                ServiceProvider.GetService<LocalServer>().Dispose();
                if (ServiceProvider.Services is IServiceProvider serviceProvider)
                {
                    foreach (IDisposable service in serviceProvider.GetServices<IDisposable>())
                    {
                        service.Dispose();
                    }
                }        
            }
            catch (Exception ex)
            {
                ServiceProvider.GetService<LogFormatter>().LogMessage($"Clossing app fialed: {ex.Message}");
            }
            finally
            {
#if DEBUG
                // Destroy Console.
                FreeConsole();
#endif
            }
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
            serviceCollection.AddSingleton(options => LocalServerExtension.StartComServer());
            serviceCollection.AddSingleton<SecureStorageService>();
            serviceCollection.AddSingleton<UserSettingsService>();
            serviceCollection.AddSingleton<IToastNotificationService, ToastNotificationService>();
            serviceCollection.AddSingleton<IDrivesService, DrivesService>();
            serviceCollection.AddSingleton(options => new LogFormatter(ServiceProvider.GetService<ILog>(), ServiceProvider.GetService<AppSettings>().AppID));

            return serviceCollection.BuildServiceProvider();
        }


        private async Task OpenItemFromProtocolUriAsync(Uri? uri)
        {
            if (uri != null)
            {
                IDrivesService domainsService = ServiceProvider.GetService<IDrivesService>();
                LogFormatter logFormatter = ServiceProvider.GetService<LogFormatter>();

                logFormatter.LogMessage($"Open item: ${uri.AbsoluteUri}");

                // Parse protocol Url.
                ProtocolParameters protocolParameters = ProtocolParameters.Parse(uri);

                // Check if domain is already mounted.           
                VirtualEngine? engine = await domainsService.EnsureEngineMountedAsync(protocolParameters.MountUrl);

                if (engine != null)
                {
                    await engine.ExecuteCommandAsync(protocolParameters);
                }
            }
        }
    }
}
