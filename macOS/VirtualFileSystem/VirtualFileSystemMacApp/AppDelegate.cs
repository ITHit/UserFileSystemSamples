using System.Diagnostics;
using Common.Core;
using FileProvider;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;
using VirtualFileSystemCommon;

namespace VirtualFilesystemMacApp
{
    [Register("AppDelegate")]
    public class AppDelegate : NSApplicationDelegate
    {
        private ILogger Logger = new ConsoleLogger("VirtualFileHostApp");
        private RemoteStorageMonitor RemoteStorageMonitor = new RemoteStorageMonitor(AppGroupSettings.Settings.Value.RemoteStorageRootPath, new ConsoleLogger(typeof(RemoteStorageMonitor).Name));
        private NSMenuItem InstallMenuItem = new NSMenuItem("Install FS Extension");
        private NSMenuItem UninstallMenuItem = new NSMenuItem("Uninstall FS Extension");
        private NSStatusItem StatusItem;
        private SecureStorage secureStorage = new SecureStorage();

        public AppDelegate()
        {
        }

        public override void DidFinishLaunching(NSNotification notification)
        {
            NSMenu menu = new NSMenu();

            Task<bool> taskIsExtensionRegistered = Task.Run<bool>(async () => await Common.Core.Registrar.IsRegisteredAsync(SecureStorage.BaseExtensionIdentifier));
            bool isExtensionRegistered = taskIsExtensionRegistered.Result;
            if (isExtensionRegistered)
            {
                UninstallMenuItem.Activated += Uninstall;
                RemoteStorageMonitor.ServerNotifications = new ServerNotifications(SecureStorage.BaseExtensionIdentifier,
                        NSFileProviderManager.FromDomain(new NSFileProviderDomain(SecureStorage.BaseExtensionIdentifier, SecureStorage.BaseExtensionDisplayName)),
                        RemoteStorageMonitor.Logger);
                RemoteStorageMonitor.Start();
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

            if (!Directory.Exists(AppGroupSettings.Settings.Value.RemoteStorageRootPath))
            {
                NSAlert alert = NSAlert.WithMessage("Path is not found", null, null, null, "");
                alert.RunModal();

                NSApplication.SharedApplication.Terminate(this);
            }           
        }    

        private void Install(object? sender, EventArgs e)
        {
            Process.Start("open", AppGroupSettings.Settings.Value.RemoteStorageRootPath);
            Task.Run(async () =>
            {
                NSFileProviderDomain domain = await Common.Core.Registrar.RegisterAsync(SecureStorage.BaseExtensionIdentifier, SecureStorage.BaseExtensionDisplayName, Logger);
                RemoteStorageMonitor.ServerNotifications = new ServerNotifications(domain.Identifier, NSFileProviderManager.FromDomain(domain), RemoteStorageMonitor.Logger);           
                RemoteStorageMonitor.Start();
            }).Wait();

            InstallMenuItem.Activated -= Install;
            InstallMenuItem.Action = null;
            UninstallMenuItem.Activated += Uninstall;
        }

        private void Uninstall(object? sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                RemoteStorageMonitor.Stop();
                await Common.Core.Registrar.UnregisterAsync(SecureStorage.BaseExtensionIdentifier, Logger);
            }).Wait();

            InstallMenuItem.Activated += Install;
            UninstallMenuItem.Activated -= Uninstall;
            UninstallMenuItem.Action = null;
        }

        public override void WillTerminate(NSNotification notification)
        {
            RemoteStorageMonitor.Dispose();
        }
    }
}
