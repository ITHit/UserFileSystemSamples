using Microsoft.UI;
using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace WebDAVDrive.Platforms.Windows.Utils
{
    public static class InteropWindowsUtil
    {
        public const int WS_EX_LAYERED = 0x80000;
        public const int WS_EX_TRANSPARENT = 0x20;
        public const int LWA_ALPHA = 0x2;

        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;

        public const int WS_MINIMIZEBOX = 0x20000;
        public const int WS_MAXIMIZEBOX = 0x10000;
        public const int WS_SYSMENU = 0x80000;
        public const int WS_POPUP = unchecked((int)0x80000000); // Correct defined as int
        public const int WS_CAPTION = 0xC00000;
        public const int WS_THICKFRAME = 0x40000;
        public const int WS_EX_TOOLWINDOW = 0x80;
        public const int SW_RESTORE = 9;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(nint hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(nint hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetLayeredWindowAttributes(nint hwnd, uint crKey, byte bAlpha, uint dwFlags);

#if DEBUG
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeConsole();
#endif

        /// <summary>
        /// Centers the specified .NET MAUI window on the screen.
        /// </summary>
        /// <param name="mauiWindow">The .NET MAUI window to center.</param>
        public static void CenterWindow(Microsoft.Maui.Controls.Window mauiWindow)
        {
            // Ensure the window handler is ready and we can access the native window handle
            var nativeWindow = mauiWindow?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;

            if (nativeWindow != null)
            {
                // Retrieve the HWND for the window
                nint hwnd = WindowNative.GetWindowHandle(nativeWindow);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

                if (appWindow != null)
                {
                    // Get the display area for calculating the center position
                    var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                    var displayRect = displayArea.WorkArea;

                    // Retrieve the window size
                    int windowWidth = appWindow.Size.Width;
                    int windowHeight = appWindow.Size.Height;

                    // Calculate the center position
                    int centerX = (displayRect.Width - windowWidth) / 2;
                    int centerY = (displayRect.Height - windowHeight) / 2;

                    // Move the window to the center position
                    appWindow.Move(new PointInt32(centerX, centerY));
                }
            }
        }

        /// <summary>
        /// Removes the minimize and maximize buttons from the specified .NET MAUI window
        /// and hides the window from the taskbar.
        /// </summary>
        /// <param name="mauiWindow">The .NET MAUI window to modify.</param>
        public static void RemoveMinimizeAndMaximizeBoxes(Microsoft.Maui.Controls.Window mauiWindow)
        {
            // Ensure the window handler is ready and we can access the native window handle
            var nativeWindow = mauiWindow?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;

            if (nativeWindow != null)
            {
                // Retrieve the HWND for the window
                nint hwnd = WindowNative.GetWindowHandle(nativeWindow);

                // Remove minimize and maximize boxes and make it a tool window (no taskbar icon)
                int style = GetWindowLong(hwnd, GWL_STYLE);
                style &= ~WS_MINIMIZEBOX;
                style &= ~WS_MAXIMIZEBOX;
                style &= ~WS_SYSMENU;
                style |= WS_POPUP;
                SetWindowLong(hwnd, GWL_STYLE, style);

                // Remove window decorations
                int windowStyle = GetWindowLong(hwnd, GWL_STYLE);
                windowStyle &= ~(WS_CAPTION | WS_THICKFRAME);
                SetWindowLong(hwnd, GWL_STYLE, windowStyle);
            }
        }

        /// <summary>
        /// Makes the specified .NET MAUI window transparent.
        /// </summary>
        /// <param name="mauiWindow">The .NET MAUI window to make transparent.</param>
        public static void SetWindowTransparent(Microsoft.Maui.Controls.Window mauiWindow)
        {
            // Ensure the window handler is ready and we can access the native window handle
            var nativeWindow = mauiWindow?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;

            if (nativeWindow != null)
            {
                // Retrieve the HWND for the window
                nint hwnd = WindowNative.GetWindowHandle(nativeWindow);

                var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
                SetLayeredWindowAttributes(hwnd, 0, 128, LWA_ALPHA);
            }
        }

        /// <summary>
        /// Hides the window icon from the title bar of the specified WinUI window.
        /// </summary>
        /// <param name="window">The WinUI window to modify.</param>
        public static void HideWindowIcon(Microsoft.UI.Xaml.Window window)
        {
            nint hWnd = WindowNative.GetWindowHandle(window);
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);
        }

        /// <summary>
        /// Brings the specified .NET MAUI window to the front of all other windows.
        /// </summary>
        /// <param name="window">The .NET MAUI window to bring to the front.</param>
        public static void BringWindowToFront(Microsoft.Maui.Controls.Window window)
        {
            var windowHandler = window.Handler as Microsoft.Maui.Handlers.WindowHandler;
            var nativeWindow = windowHandler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (nativeWindow != null)
            {
                nint hWnd = WindowNative.GetWindowHandle(nativeWindow);

                // Bring the window to the foreground using native Win32 API
                SetForegroundWindow(hWnd);
                ShowWindow(hWnd, SW_RESTORE);
            }
        }
    }
}
