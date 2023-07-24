using System.Diagnostics;
using Common.Core;
using FileProvider;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;
using VirtualFilesystemCommon;

namespace VirtualFilesystemMacApp
{
    [Register("AppDelegate")]
    public class AppDelegate : NSApplicationDelegate
    {
        private ILogger Logger = new ConsoleLogger("VirtualFileHostApp");
        private string ExtensionIdentifier = "com.userfilesystem.vfs.app";
        private string ExtensionDisplayName = "IT Hit File System";
        private NSMenuItem InstallMenuItem = new NSMenuItem("Install FS Extension");
        private NSMenuItem UninstallMenuItem = new NSMenuItem("Uninstall FS Extension");
        private NSStatusItem StatusItem;

        public AppDelegate()
        {
        }

        public override void DidFinishLaunching(NSNotification notification)
        {
            NSMenu menu = new NSMenu();

            Task<bool> taskIsExtensionRegistered = Task.Run<bool>(async () => await Common.Core.Registrar.IsRegisteredAsync(ExtensionIdentifier));
            bool isExtensionRegistered = taskIsExtensionRegistered.Result;
            if (isExtensionRegistered)
            {
                UninstallMenuItem.Activated += Uninstall;
            }
            else
            {
                InstallMenuItem.Activated += Install;
            }

            NSMenuItem exitMenuItem = new NSMenuItem("Quit", (a, b) => { NSApplication.SharedApplication.Terminate(this); });

            menu.AddItem(InstallMenuItem);
            menu.AddItem(UninstallMenuItem);
            menu.AddItem(exitMenuItem);

            StatusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(30);
            StatusItem.Menu = menu;
            StatusItem.Image = NSImage.ImageNamed("TrayIcon.png");
            StatusItem.HighlightMode = true;

            if (!Directory.Exists(AppGroupSettings.GetRemoteRootPath()))
            {
                NSAlert alert = NSAlert.WithMessage("Path is not found", null, null, null, "");
                alert.RunModal();

                NSApplication.SharedApplication.Terminate(this);
            }
        }

        private void Install(object? sender, EventArgs e)
        {
            Process.Start("open", AppGroupSettings.GetRemoteRootPath());
            Task.Run(async () =>
            {
                await Common.Core.Registrar.RegisterAsync(ExtensionIdentifier, ExtensionDisplayName, Logger);
            }).Wait();

            InstallMenuItem.Activated -= Install;
            InstallMenuItem.Action = null;
            UninstallMenuItem.Activated += Uninstall;
        }

        private void Uninstall(object? sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                await Common.Core.Registrar.UnregisterAsync(ExtensionIdentifier, Logger);
            }).Wait();

            InstallMenuItem.Activated += Install;
            UninstallMenuItem.Activated -= Uninstall;
            UninstallMenuItem.Action = null;
        }

        public override void WillTerminate(NSNotification notification)
        {           
        }
    }
}
