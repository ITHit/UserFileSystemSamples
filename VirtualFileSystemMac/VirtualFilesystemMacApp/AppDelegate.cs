using AppKit;
using Foundation;
using System.IO;
using VirtualFilesystemCommon;

namespace VirtualFilesystemMacApp
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
            LocalExtensionManager = new ExtensionManager("com.ithit.virtualfilesystem.app", "ITHitFS");

            NSMenu menu = new NSMenu();

            NSMenuItem installMenuItem = new NSMenuItem("Install FS Extension", (a, b) => { LocalExtensionManager.InstallExtension(); });
            NSMenuItem uninstallMenuItem = new NSMenuItem("Uninstall FS Extension", (a, b) => { LocalExtensionManager.UninstallExtension(); });
            NSMenuItem exitMenuItem = new NSMenuItem("Quit", (a, b) => { NSApplication.SharedApplication.Terminate(this); });

            menu.AddItem(installMenuItem);
            menu.AddItem(uninstallMenuItem);
            menu.AddItem(exitMenuItem);

            StatusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(30);
            StatusItem.Menu = menu;
            StatusItem.Image = NSImage.ImageNamed("TrayIcon.png");
            StatusItem.HighlightMode = true;

            NSDictionary userData = AppGroupSettings.SaveSharedSettings("appsettings.json");
            if (!Directory.Exists(userData[AppGroupSettings.LocalPathId] as NSString))
            {
                NSAlert alert = NSAlert.WithMessage("Path is not found", null, null, null, "");
                alert.RunModal();

                NSApplication.SharedApplication.Terminate(this);
            }
        }

        public override void WillTerminate(NSNotification notification)
        {
        }
    }
}
