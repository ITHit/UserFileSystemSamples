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
    public class ConsoleManager
    {
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// Hides or hides console window.
        /// </summary>
        /// <param name="visible">Console visibility.</param>
        public static void SetConsoleWindowVisibility(bool visible)
        {
            IntPtr hWnd = FindWindow(null, Console.Title);
            if (hWnd != IntPtr.Zero)
            {
                if (visible) ShowWindow(hWnd, 1);       
                else ShowWindow(hWnd, 0);               
            }
        }
    }
}
