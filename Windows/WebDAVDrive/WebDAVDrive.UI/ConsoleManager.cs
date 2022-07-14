using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace WebDAVDrive.UI
{
    /// <summary>
    /// Console helper methods.
    /// </summary>
    public static class ConsoleManager
    {
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
    }
}
