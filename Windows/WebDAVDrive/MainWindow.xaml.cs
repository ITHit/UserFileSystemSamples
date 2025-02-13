using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using System;
using Windows.Graphics;
using WinUIEx;

namespace WebDAVDrive
{
    /// <summary>
    /// Main application window. This window is hidden and is requitred only to process application message loop.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetDpiForWindow(IntPtr hwnd);

        public MainWindow()
        {
            InitializeComponent();
            //listen to "theme changed" event on main window's root element, because WinUI does not support that event on Application or Window level
            MainStackPanel.ActualThemeChanged += MainStackPanel_ActualThemeChanged;

            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            int dpi = GetDpiForWindow(hWnd);
            ServiceProvider.Scale = dpi * 100 / 96;

            AppWindow.Move(new PointInt32(-100000, 0));
            this.SetIsShownInSwitchers(false);
        }

        private void MainStackPanel_ActualThemeChanged(FrameworkElement sender, object args)
        {
            //assign IsDarkTheme property on theme changing
            ServiceProvider.IsDarkTheme = Application.Current.RequestedTheme == ApplicationTheme.Dark;
        }
    }

}
