using System.Windows.Input;
using WebDAVDrive.Platforms.Windows.Utils;

namespace WebDAVDrive;

public partial class StartupPage : ContentPage
{
    public StartupPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    public ICommand TapCommand => new Command(DialogsUtil.OpenMountNewDriveWindow);


    private void OnCloseClicked(object sender, EventArgs e)
    {
        if (cbDoNotShowAgain.IsChecked)
        {
            Preferences.Set("StartupWindowDoNotShowAgain", true);
        }

        Application.Current.CloseWindow(Window);
    }
}
