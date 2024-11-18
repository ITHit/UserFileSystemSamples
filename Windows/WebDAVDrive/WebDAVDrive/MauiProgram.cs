using log4net;
using Microsoft.Extensions.Configuration;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Windows.AppLifecycle;
using System.Reflection;
using Windows.ApplicationModel.Activation;

using ITHit.FileSystem.Samples.Common.Windows;

using WebDAVDrive.Extensions;
using WebDAVDrive.Platforms.Windows.Extensions;
using WebDAVDrive.Platforms.Windows.Services;
using WebDAVDrive.Platforms.Windows.Utils;
using WebDAVDrive.Services;
using WebDAVDrive.ShellExtension;
using WebDAVDrive.Utils;
using ITHit.FileSystem.Windows.ShellExtension;
using Microsoft.Maui;

namespace WebDAVDrive
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false, true)
                .Build();

            builder.Configuration.AddConfiguration(config);
            builder.Services.AddSingleton(options => LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType));
            builder.Services.AddSingleton(options => config.ReadSettings());
            builder.Services.AddSingleton(options => ShellExtensions.StartComServer());
            builder.Services.AddSingleton<IToastNotificationService, ToastNotificationService>();
            builder.Services.AddSingleton<IDomainsService, DomainsService>();
            builder.Services.AddSingleton(options => new LogFormatter(ServiceProviderUtil.GetService<ILog>(), ServiceProviderUtil.GetService<AppSettings>().AppID));

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.ConfigureLifecycleEvents(events =>
            {
                // Make sure to add "using Microsoft.Maui.LifecycleEvents;" in the top of the file
                events.AddWindows(configure =>
                {
                    configure.OnWindowCreated(window =>
                    {
                        if (Application.Current?.Windows.Count == 1)
                        {
                            window.Closed += MainWindowClosed;
                        }
                    });
                    configure.OnLaunched((_, _) =>
                    {
                        AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
                        IDomainsService domainsService = ServiceProviderUtil.GetService<IDomainsService>();

                        // We want the application to continue processing events, while engines are mounted and starting.
                        _ = Task.Run(async () =>
                        {
                            // Init engines.                        
                            await domainsService.InitializeAsync((args.Data as ProtocolActivatedEventArgs)?.Uri == null);

                            // Open url if app was opened via protocol.
                            await OpenItemFromProtocolUriAsync((args.Data as ProtocolActivatedEventArgs)?.Uri);
#if DEBUG
                            foreach (VirtualEngine engine in domainsService.Engines.Values)
                            {
                                Commands.Open(engine.Path);
                                Commands.Open(engine.RemoteStorageRootPath); // Open remote storage.
                            }
#endif

                        });
#if !DEBUG
                        if (!Preferences.Get("StartupWindowDoNotShowAgain", false))
                        {
                            _ = MainThread.InvokeOnMainThreadAsync(() =>
                            {
                                DialogsUtil.OpenStartupWindow();
                            });
                        }
#endif
                        _ = Task.Run(async () =>
                        {
                            // Print console commands.
                            domainsService.ConsoleProcessor.PrintHelp();

                            // Read user input.
                            await domainsService.ConsoleProcessor.ProcessUserInputAsync(() =>
                            {
                                MainThread.InvokeOnMainThreadAsync(() =>
                                {
                                    Application.Current!.Quit();
                                });
                            });
                        });
                    });

                    configure.OnAppInstanceActivated(async (sender, e) =>
                    {
                        if (e.Kind == ExtendedActivationKind.Protocol)
                        {
                            await OpenItemFromProtocolUriAsync((e.Data as ProtocolActivatedEventArgs)?.Uri);
                        }
                    });
                });
            });

            builder.Logging.AddLog4Net("log4net.config");

            MauiApp app = builder.Build();

            // Set the service provider
            ServiceProviderUtil.ServiceProvider = app.Services;

#if DEBUG
            // Display Console
            InteropWindowsUtil.AllocConsole();
            WindowManager.ConfigureConsole();     
#endif

            // Log environment
            app.Services.GetService<LogFormatter>()!.PrintEnvironmentDescription();


            return app;
        }

        private static void MainWindowClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
        {
            ServiceProviderUtil.GetService<IDomainsService>().EnginesExitAsync().Wait();

            // Explicitly call Dispose for LocalServer.
            ServiceProviderUtil.GetService<LocalServer>().Dispose();
            if (ServiceProviderUtil.ServiceProvider is IServiceProvider serviceProvider)
            {
                foreach (var service in serviceProvider.GetServices<IDisposable>())
                {
                    service.Dispose();
                }
            }
#if DEBUG
            // Destroy Console.
            InteropWindowsUtil.FreeConsole();
#endif
        }

        static async Task OpenItemFromProtocolUriAsync(Uri? uri)
        {
            if (uri != null)
            {
                IDomainsService domainsService = ServiceProviderUtil.GetService<IDomainsService>();
                LogFormatter logFormatter = ServiceProviderUtil.GetService<LogFormatter>();

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
