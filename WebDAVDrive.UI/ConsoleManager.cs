using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace WebDAVDrive.UI
{
    public class ConsoleManager
    {
        /// <summary>
        /// ConsoleExitEvent, works as ManualResetEvent. Contain field KeyInfo with ConsoleKeyInfo, which contains info about key, which was pressed to exit from application.
        /// </summary>
        public class ConsoleExitEvent : EventWaitHandle
        {
            /// <summary>
            /// Key, used to select exit method.
            /// </summary>
            public ConsoleKeyInfo KeyInfo { get; set; }

            public ConsoleExitEvent():base(false, EventResetMode.ManualReset)
            {
                KeyInfo = new ConsoleKeyInfo();
            }
        }

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// Hide/Show console.
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

        /// <summary>
        /// Starts new thread and waits while any key will be pressed in console.
        /// </summary>
        /// <param name="exitEvent">ManualResetEvent, invokes when any key will be pressed.</param>
        /// <returns></returns>
        public static void WaitConsoleReadKey(ConsoleExitEvent exitEvent) 
        {
            ConsoleKeyInfo exitKey = new ConsoleKeyInfo();
            Thread readKeyThread = new Thread(() =>
            {
                exitKey = Console.ReadKey();
                exitEvent.KeyInfo = (ConsoleKeyInfo)exitKey;
                exitEvent.Set();
            });
            readKeyThread.IsBackground = true;
            readKeyThread.Start();
        }
    }
}
