using Microsoft.Maui.LifecycleEvents;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.Diagnostics;
using WebDAVDrive.Platforms.Windows.Extensions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WebDAVDrive.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            var singleInstance = AppInstance.FindOrRegisterForKey("SingleInstanceApp");
            if (!singleInstance.IsCurrent)
            {
                // this is another instance

                // 1. activate the first instance
                var currentInstance = AppInstance.GetCurrent();
                var args = currentInstance.GetActivatedEventArgs();
                singleInstance.RedirectActivationToAsync(args).GetAwaiter().GetResult();

                // 2. close this instance
                Process.GetCurrentProcess().Kill();
                return;
            }

            // this is the first instance
            // 1. register for future activation
            singleInstance.Activated += OnAppInstanceActivated;

            this.InitializeComponent();
        }

        private void OnAppInstanceActivated(object? sender, AppActivationArguments e)
        {
            Services.GetRequiredService<ILifecycleEventService>().OnAppInstanceActivated(sender, e);
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
