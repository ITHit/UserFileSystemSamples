using AppKit;
using Foundation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WebDAVCommon;

namespace WebDAVMacApp
{
    [Register("AppDelegate")]
    public class AppDelegate : NSApplicationDelegate
    {
        private ExtensionManager LocalExtensionManager;
        private NSStatusItem StatusItem;

        public AppDelegate()
        {
        }

        public override void DidFinishLaunching(NSNotification notification)
        {
            LocalExtensionManager = new ExtensionManager("com.webdav.vfs.app", "ITHitWebDAVFS");

            NSMenu menu = new NSMenu();

            NSMenuItem installMenuItem = new NSMenuItem("Install WebDAV FS Extension", (a, b) =>
            {
                Process.Start(AppGroupSettings.GetWebDAVServerUrl());
                LocalExtensionManager.InstallExtension();
            });
            NSMenuItem uninstallMenuItem = new NSMenuItem("Uninstall WebDAV FS Extension", (a, b) => { LocalExtensionManager.UninstallExtension(); });
            NSMenuItem exitMenuItem = new NSMenuItem("Quit", (a, b) => { NSApplication.SharedApplication.Terminate(this); });

            menu.AddItem(installMenuItem);
            menu.AddItem(uninstallMenuItem);
            menu.AddItem(exitMenuItem);

            StatusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(30);
            StatusItem.Menu = menu;
            StatusItem.Image = NSImage.ImageNamed("TrayIcon.png");
            StatusItem.HighlightMode = true;

            NSDictionary userData = AppGroupSettings.SaveSharedSettings("appsettings.json");
            if (string.IsNullOrEmpty(userData[AppGroupSettings.WebDAVServerUrlId] as NSString))
            {
                NSAlert alert = NSAlert.WithMessage("WebDAV Server not found.", null, null, null, "");
                alert.RunModal();

                NSApplication.SharedApplication.Terminate(this);
            }

        }

        public static string MapPath(string userFileSystemPath)
        {
            // Get path relative to the virtual root.
            string relativePath = userFileSystemPath.TrimEnd(Path.DirectorySeparatorChar).Substring(
                AppGroupSettings.GetUserRootPath().TrimEnd(Path.DirectorySeparatorChar).Length);
            relativePath = relativePath.TrimStart(Path.DirectorySeparatorChar);

            string[] segments = relativePath.Split('/');

            IEnumerable<string> encodedSegments = segments.Select(x => Uri.EscapeDataString(x));
            relativePath = string.Join('/', encodedSegments);

            string path = $"{AppGroupSettings.GetWebDAVServerUrl()}{relativePath}";

            return path;
        }

        public override void WillTerminate(NSNotification notification)
        {
        }
    }
}
