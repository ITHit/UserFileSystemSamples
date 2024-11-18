using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebDAVDrive.Platforms.Windows.Utils
{
    internal class DialogsUtil
    {
        public static void OpenMountNewDriveWindow()
        {
            Window mountNewDriveWindow = new Window
            {
                Title = $"WebDAV Drive - Mount New Drive",
                Page = new MountNewDrivePage()
            };
            mountNewDriveWindow.Width = 650;
            mountNewDriveWindow.Height = 300;
            mountNewDriveWindow.X = -10000;
            Application.Current.OpenWindow(mountNewDriveWindow);
            InteropWindowsUtil.RemoveMinimizeAndMaximizeBoxes(mountNewDriveWindow);
            InteropWindowsUtil.CenterWindow(mountNewDriveWindow);
            InteropWindowsUtil.BringWindowToFront(mountNewDriveWindow);
        }

        public static void OpenStartupWindow()
        {
            Window startupWindow = new Window
            {
                Title = $"WebDAV Drive - Startup",
                Page = new StartupPage()
            };
            startupWindow.Width = 650;
            startupWindow.Height = 300;
            startupWindow.X = -10000;
            Application.Current.OpenWindow(startupWindow);
            InteropWindowsUtil.RemoveMinimizeAndMaximizeBoxes(startupWindow);
            InteropWindowsUtil.CenterWindow(startupWindow);
            InteropWindowsUtil.BringWindowToFront(startupWindow);
        }
    }
}
