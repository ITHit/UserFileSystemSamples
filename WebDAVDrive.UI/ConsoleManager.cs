using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace WebDAVDrive.UI
{
    public class ConsoleManager
    {
        #region console-show-hide
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        /// <summary>
        /// Hide/Show console
        /// </summary>
        /// <param name="visible">Console visibility</param>
        public static void SetConsoleWindowVisibility(bool visible)
        {
            IntPtr hWnd = FindWindow(null, Console.Title);
            if (hWnd != IntPtr.Zero)
            {
                if (visible) ShowWindow(hWnd, 1); //1 = SW_SHOWNORMAL           
                else ShowWindow(hWnd, 0); //0 = SW_HIDE               
            }
        }
        #endregion

        /// <summary>
        /// Starts new thread and waits while any key will be pressed in console.
        /// </summary>
        /// <param name="exitEvent">ManualResetEvent, invokes when any key will be pressed</param>
        /// <returns></returns>
        public static ConsoleKeyInfo WaitConsoleReadKey(ManualResetEvent exitEvent) 
        {
            ConsoleKeyInfo exitKey = new ConsoleKeyInfo();
            Thread readKeyThread = new Thread(() =>
            {
                exitKey = Console.ReadKey();
                exitEvent.Set();
            });
            readKeyThread.IsBackground = true;
            readKeyThread.Start();
            return exitKey;
        }
    }
}
