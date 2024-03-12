using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Window helper methods.
    /// </summary>
    public static class WindowManager

    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT Rect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int Width, int Height, bool Repaint);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);


        /// <summary>
        /// Console visibility.
        /// </summary>
        public static bool ConsoleVisible { get; private set; }
#if !DEBUG
            = false;
#else
            = true;
#endif

        /// <summary>
        /// Hides or hides console window.
        /// </summary>
        /// <param name="visible">Console visibility.</param>
        public static void SetConsoleWindowVisibility(bool setVisible)
        {
            IntPtr hWnd = FindWindow(null, Console.Title);
            if (hWnd != IntPtr.Zero)
            {
                ShowWindow(hWnd, setVisible ? 1 : 0);
                ConsoleVisible = setVisible;
            }
        }

#if DEBUG
        /// <summary>
        /// Stratches console window to the width of the screen and positions it at the bottom of the screen.
        /// </summary>
        private static void PositionConsoleWindow()
        {
            IntPtr hWnd = FindWindow(null, Console.Title);
            if (hWnd != IntPtr.Zero)
            {
                RECT rect = new RECT();
                if (GetWindowRect(hWnd, ref rect))
                {
                    var screen = System.Windows.Forms.Screen.FromHandle(hWnd);
                    int height = screen.WorkingArea.Height / 4;
                    MoveWindow(hWnd, 0, screen.WorkingArea.Height - height, screen.WorkingArea.Width, height, true);
                }
            }
        }

        public static void PositionFileSystemWindow(IntPtr hWnd, int horizontalIndex, int totalWindows)
        {
            if (hWnd != IntPtr.Zero)
            {
                RECT rect = new RECT();
                if (GetWindowRect(hWnd, ref rect))
                {
                    var screen = System.Windows.Forms.Screen.FromHandle(hWnd);
                    int height = (screen.WorkingArea.Height / 4)*3;
                    int width = (screen.WorkingArea.Width / totalWindows);
                    int x = width * horizontalIndex;
                    MoveWindow(hWnd, x, 0, width, height, true);
                }
            }
        }

        public static IntPtr FindWindow(string name, CancellationToken cancellationToken = default)
        {
            IntPtr hWnd = IntPtr.Zero;
            do
            {
                hWnd = FindWindow(null, name);
                if (hWnd != IntPtr.Zero)
                {
                    return hWnd;
                }
                Thread.Sleep(100);
            } while(hWnd == IntPtr.Zero && !cancellationToken.IsCancellationRequested);
            return IntPtr.Zero;
        }

        /// <summary>
        /// Sets console output defaults.
        /// </summary>
        public static void ConfigureConsole()
        {
            // Enable UTF8 for Console Window and set width.
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.SetWindowSize(Console.LargestWindowWidth, Console.LargestWindowHeight / 3);
            //Console.SetBufferSize(Console.LargestWindowWidth * 2, short.MaxValue / 2);

            PositionConsoleWindow();
        }
#endif
    }
}
