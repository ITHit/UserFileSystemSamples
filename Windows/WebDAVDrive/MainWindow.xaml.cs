using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinUIEx;

namespace WebDAVDrive
{
    /// <summary>
    /// Main application window. This window is hidden and is requitred only to process application message loop.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            AppWindow.Move(new PointInt32(-100000, 0));
            this.SetIsShownInSwitchers(false);
        }
    }
}
