using WebDAVDrive.Platforms.Windows.Utils;

namespace WebDAVDrive
{
    public partial class SettingsPage : ContentPage
    {

        public SettingsPage()
        {
            InitializeComponent();
            BindingContext = this;

            // set width and height of window
            Window? window = Application.Current?.MainPage?.Window;
            if (window != null)
            {
                window.Width = 350;
                window.Height = 650;
                window.X = -100000;
            }
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
            InteropWindowsUtil.HideWindowIcon((Microsoft.UI.Xaml.Window)App.Current.Windows.First<Window>().Handler.PlatformView!);
        }       
    }

}
