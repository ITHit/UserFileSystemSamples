using Microsoft.UI.Xaml;
using Windows.ApplicationModel.Resources;
using WinUIEx;

namespace WebDAVDrive.Dialogs
{
    /// <summary>
    /// Aplication startup dialog.
    /// </summary>
    public sealed partial class Startup : DialogWindow
    {
        public Startup() : base()
        {
            InitializeComponent();
            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            Title = $"{ServiceProvider.GetService<AppSettings>().ProductName} - {resourceLoader.GetString("Startup/Title")}";

            // Resize and center the window.
            SetDefaultSizePosition();
        }

        private void OnMountNewDriveClicked(object sender, RoutedEventArgs e)
        {
            // Open the MountNewDrive dialog
            new MountNewDrive().Show();
            Close();
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            if (cbDoNotShowAgain.IsChecked == true)
            {
                Windows.Storage.ApplicationData.Current.LocalSettings.Values["StartupWindowDoNotShowAgain"] = true;
            }

            Close();
        }

        //focus Close button, make it trigger click by Enter press
        private void CloseButtonLoaded(object sender, RoutedEventArgs e)
        {
            CloseButton.Focus(FocusState.Programmatic);
        }
    }
}
