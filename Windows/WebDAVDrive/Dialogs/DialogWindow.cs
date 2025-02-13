using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;
using WinUIEx;

namespace WebDAVDrive.Dialogs
{
    /// <summary>
    /// Base class for dialog windows.
    /// </summary>
    public class DialogWindow : Window
    {
        private bool keyDownEventAttached = false;

        public DialogWindow()
        {
            Activated += OnWindowActivated;

            // Set the window icon
            AppWindow.SetIcon("Images/AppIcon.ico");
        }

        protected void SetDefaultPosition()
        {            
            this.SetIsResizable(false);
            this.SetIsMaximizable(false);
            this.SetIsMinimizable(false);
            CenterWindow();
            this.SetForegroundWindow();
        }

        //Assign KeyDown event (for Esc handling) on window's first activation (when Content is already defined).
        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (!keyDownEventAttached && args.WindowActivationState != WindowActivationState.Deactivated)
            {
                Content.KeyDown += OnRootContentKeyDown;
                keyDownEventAttached = true;
            }
        }

        //Close window on Esc key press.
        private void OnRootContentKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                Close();
            }
        }

        /// <summary>
        /// Centers current dialog window on the screen.
        /// </summary>
        public void CenterWindow()
        {
            nint hwnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

            // Get the display area for calculating the center position
            DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            RectInt32 displayRect = displayArea.WorkArea;

            // Retrieve the window size
            int windowWidth = AppWindow.Size.Width;
            int windowHeight = AppWindow.Size.Height;

            // Calculate the center position
            int centerX = (displayRect.Width - windowWidth) / 2;
            int centerY = (displayRect.Height - windowHeight) / 2;

            // Move the window to the center position
            AppWindow.Move(new PointInt32(centerX, centerY));
        }
    }
}
