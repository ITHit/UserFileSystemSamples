using System.Diagnostics;
using AppKit;
using Common.Core;
using FileProvider;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;
using WebDAVCommon;

namespace WebDAVMacApp
{
    [Register("AppDelegate")]
    public class AppDelegate : NSApplicationDelegate
    {
        private ILogger Logger = new ConsoleLogger("WebDavFileProviderHostApp");
        private RemoteStorageMonitor RemoteStorageMonitor = new RemoteStorageMonitor(AppGroupSettings.Settings.Value.WebSocketServerUrl, new ConsoleLogger(typeof(RemoteStorageMonitor).Name));
        private string ExtensionIdentifier = "com.webdav.vfs.app";
        private string ExtensionDisplayName = "IT Hit WebDAV Drive";
        private NSMenuItem InstallMenuItem = new NSMenuItem("Install WebDAV FS Extension");
        private NSMenuItem UninstallMenuItem = new NSMenuItem("Uninstall WebDAV FS Extension");
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
                Task.Run(async () =>
                {
                    RemoteStorageMonitor.ServerNotifications = new ServerNotifications(
                        NSFileProviderManager.FromDomain(new NSFileProviderDomain(ExtensionIdentifier, ExtensionDisplayName)),
                        RemoteStorageMonitor.Logger);
                    await RemoteStorageMonitor.StartAsync();
                }).Wait();
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

            //AppGroupSettings.SaveSharedSettings("appsettings.json");
            if (string.IsNullOrEmpty(AppGroupSettings.Settings.Value.WebDAVServerUrl))
            {
                NSAlert alert = NSAlert.WithMessage("WebDAV Server not found.", null, null, null, "");
                alert.RunModal();

                NSApplication.SharedApplication.Terminate(this);
            }
        }

        private void Install(object? sender, EventArgs e)
        {
            Process.Start("open", AppGroupSettings.Settings.Value.WebDAVServerUrl);
            Task.Run(async () =>
            {
                NSFileProviderDomain domain = await Common.Core.Registrar.RegisterAsync(ExtensionIdentifier, ExtensionDisplayName, Logger);
                RemoteStorageMonitor.ServerNotifications = new ServerNotifications(NSFileProviderManager.FromDomain(domain), RemoteStorageMonitor.Logger);
                await RemoteStorageMonitor.StartAsync();
            }).Wait();

            InstallMenuItem.Activated -= Install;
            InstallMenuItem.Action = null;
            UninstallMenuItem.Activated += Uninstall;
        }

        private void Uninstall(object? sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                await RemoteStorageMonitor.StopAsync();
                await Common.Core.Registrar.UnregisterAsync(ExtensionIdentifier, Logger);
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
