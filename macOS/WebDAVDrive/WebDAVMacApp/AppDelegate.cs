using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
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
        private RemoteStorageMonitor? RemoteStorageMonitor = null;
        private NSMenuItem InstallMenuItem = new NSMenuItem("Install WebDAV FS Extension");
        private NSMenuItem UninstallMenuItem = new NSMenuItem("Uninstall WebDAV FS Extension");
        private NSStatusItem StatusItem;
        private SecureStorage SecureStorage = new SecureStorage();

        public AppDelegate()
        {
        }

        public override void DidFinishLaunching(NSNotification notification)
        {
            NSMenu menu = new NSMenu();

            string domainIdentifier = Task.Run<string>(async () => await SecureStorage.GetAsync("CurrentDomainIdentifier")).Result;
            if (string.IsNullOrEmpty(domainIdentifier))
            {
                domainIdentifier = SecureStorage.ExtensionIdentifier;
            }

            Task<bool> taskIsExtensionRegistered = Task.Run<bool>(async () => await Common.Core.Registrar.IsRegisteredAsync(domainIdentifier));
            bool isExtensionRegistered = taskIsExtensionRegistered.Result;
            if (isExtensionRegistered)
            {
                UninstallMenuItem.Activated += Uninstall;
                Task.Run(async () =>
                {
                    // Get WebDAVServerUrl and WebSocketServerUrl
                    string webDAVServerUrl = AppGroupSettings.Settings.Value.WebDAVServerUrl;
                    string webSocketServerUrl = AppGroupSettings.Settings.Value.WebSocketServerUrl;
                    DomainSettings domainSettings = await SecureStorage.GetAsync<DomainSettings>(domainIdentifier);
                    if (domainSettings != null && !string.IsNullOrEmpty(domainSettings.WebDAVServerUrl))
                    {
                        webDAVServerUrl = domainSettings.WebDAVServerUrl;
                        webSocketServerUrl = domainSettings.WebSocketServerUrl;
                    }

                    NSFileProviderManager fileProviderManager = NSFileProviderManager.FromDomain(new NSFileProviderDomain(domainIdentifier, SecureStorage.ExtensionDisplayName));
                    RemoteStorageMonitor = new RemoteStorageMonitor(webDAVServerUrl, webSocketServerUrl, fileProviderManager, new ConsoleLogger(typeof(RemoteStorageMonitor).Name));
                    RemoteStorageMonitor.ServerNotifications = new ServerNotifications(fileProviderManager, RemoteStorageMonitor.Logger);
                    await RemoteStorageMonitor.StartAsync();

                    if (NSProcessInfo.ProcessInfo.Arguments.Length > 1)
                    {
                        // Update the domain identifier if the current identifier differs from the arguments.
                        Uri webDavRootUrl = new Uri(NSProcessInfo.ProcessInfo.Arguments[1]);
                        domainIdentifier = webDavRootUrl.Host;

                        await CheckDomainAndOpenItemAsync(domainIdentifier, NSProcessInfo.ProcessInfo.Arguments[1], NSProcessInfo.ProcessInfo.Arguments[2]);
                    }
                }).Wait();
            }
            else if (NSProcessInfo.ProcessInfo.Arguments.Length > 1)
            {
                Logger.LogDebug($"Arguments: {string.Join(",", NSProcessInfo.ProcessInfo.Arguments.Skip(1))}");
                Uri webDavRootUrl = new Uri(NSProcessInfo.ProcessInfo.Arguments[1]);
                domainIdentifier = webDavRootUrl.Host;

                Task.Run(async () =>
                {
                    await SecureStorage.SetAsync(domainIdentifier,
                        new DomainSettings
                        {
                            WebDAVServerUrl = NSProcessInfo.ProcessInfo.Arguments[1],
                            WebSocketServerUrl = $"wss://{webDavRootUrl.Host}/"
                        });
                    await CreateDomainAsync(domainIdentifier, SecureStorage.ExtensionDisplayName, false);
                    await OpenItemAsync(domainIdentifier, NSProcessInfo.ProcessInfo.Arguments[1], NSProcessInfo.ProcessInfo.Arguments[2]);
                }).Wait();

                UninstallMenuItem.Activated += Uninstall;
            }
            else
            {
                InstallMenuItem.Activated += Install;
            }

            NSMenuItem exitMenuItem = new NSMenuItem("Quit", (a, b) =>
            {
                Task.Run(async () =>
                {
                    if (RemoteStorageMonitor != null && RemoteStorageMonitor.SyncState == SynchronizationState.Enabled)
                    {
                        string domainIdentifier = await SecureStorage.GetAsync("CurrentDomainIdentifier");
                        if (string.IsNullOrEmpty(domainIdentifier))
                        {
                            domainIdentifier = SecureStorage.ExtensionIdentifier;
                        }
                        await RemoteStorageMonitor?.StopAsync();
                    }
                }).Wait();
                NSApplication.SharedApplication.Terminate(this);
            });

            menu.AddItem(InstallMenuItem);
            menu.AddItem(UninstallMenuItem);
            menu.AddItem(exitMenuItem);

            StatusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(30);
            StatusItem.Menu = menu;
            StatusItem.Image = NSImage.ImageNamed("TrayIcon.png");
            StatusItem.HighlightMode = true;

            // Subscribe to Notification from protocol handler.
            NSDistributedNotificationCenter.DefaultCenter.AddObserver(new NSString("ITHitWebDavOpenItem"), OpenItemNotificationHandlerAsync);
            NSDistributedNotificationCenter.DefaultCenter.AddObserver(new NSString("ITHitWebDavMountNewDomainAndOpenItem"), MountNewDomainAndOpenItemNotificationHandlerAsync);
        }

        private void Install(object? sender, EventArgs e)
        {

            if (Task.Run<bool>(async () =>
            {
                string domainIdentifier = await SecureStorage.GetAsync("CurrentDomainIdentifier");
                if (string.IsNullOrEmpty(domainIdentifier))
                {
                    domainIdentifier = SecureStorage.ExtensionIdentifier;
                }

                if (await SecureStorage.GetAsync<DomainSettings>(domainIdentifier) == null)
                {
                    await SecureStorage.SetAsync(domainIdentifier,
                        new DomainSettings
                        {
                            WebDAVServerUrl = AppGroupSettings.Settings.Value.WebDAVServerUrl,
                            WebSocketServerUrl = AppGroupSettings.Settings.Value.WebSocketServerUrl
                        });
                }
                return await CreateDomainAsync(domainIdentifier, SecureStorage.ExtensionDisplayName, true);
            }).Result)
            {
                InstallMenuItem.Activated -= Install;
                InstallMenuItem.Action = null;
                UninstallMenuItem.Activated += Uninstall;
            }
        }

        private void Uninstall(object? sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                string domainIdentifier = await SecureStorage.GetAsync("CurrentDomainIdentifier");
                if (string.IsNullOrEmpty(domainIdentifier))
                {
                    domainIdentifier = SecureStorage.ExtensionIdentifier;
                }

                await RemoteStorageMonitor?.StopAsync();
                await Common.Core.Registrar.UnregisterAsync(domainIdentifier, Logger);
            }).Wait();

            InstallMenuItem.Activated += Install;
            UninstallMenuItem.Activated -= Uninstall;
            UninstallMenuItem.Action = null;
        }

        public override void WillTerminate(NSNotification notification)
        {
            RemoteStorageMonitor?.Dispose();
            RemoteStorageMonitor = null;
        }

        private async void OpenItemNotificationHandlerAsync(NSNotification n)
        {
            NotificationItemSettings notificationItemSettings = JsonSerializer.Deserialize<NotificationItemSettings>(n.Object.ToString());
            Uri webDavRootUrl = new Uri(notificationItemSettings.MountUrl);
            string domainIdentifier = webDavRootUrl.Host;

            Task<bool> taskIsExtensionRegistered = Task.Run<bool>(async () => await Common.Core.Registrar.IsRegisteredAsync(await SecureStorage.GetAsync("CurrentDomainIdentifier")));
            if (taskIsExtensionRegistered.Result)
            {
                await CheckDomainAndOpenItemAsync(domainIdentifier, notificationItemSettings.MountUrl, notificationItemSettings.DocumentUrl);
            }
            else
            {
                // Create domain if not register.
                Task.Run(async () =>
                {
                    await SecureStorage.SetAsync(domainIdentifier,
                        new DomainSettings
                        {
                            WebDAVServerUrl = notificationItemSettings.MountUrl,
                            WebSocketServerUrl = $"wss://{webDavRootUrl.Host}/"
                        });
                    await CreateDomainAsync(domainIdentifier, SecureStorage.ExtensionDisplayName, false);

                    InstallMenuItem.Activated -= Install;
                    InstallMenuItem.Action = null;
                    UninstallMenuItem.Activated += Uninstall;

                    await OpenItemAsync(domainIdentifier, notificationItemSettings.MountUrl, notificationItemSettings.DocumentUrl);
                }).Wait();
            }
        }

        private async void MountNewDomainAndOpenItemNotificationHandlerAsync(NSNotification n)
        {
            NotificationItemSettings notificationItemSettings = JsonSerializer.Deserialize<NotificationItemSettings>(n.Object.ToString());

            Uninstall(null, null);
            Uri webDavRootUri = new Uri(notificationItemSettings.MountUrl);
            string domainIdentifier = webDavRootUri.Host;

            await SecureStorage.SetAsync(domainIdentifier,
                        new DomainSettings
                        {
                            WebDAVServerUrl = notificationItemSettings.MountUrl,
                            WebSocketServerUrl = $"wss://{webDavRootUri.Host}/"
                        });
            await CreateDomainAsync(domainIdentifier, SecureStorage.ExtensionDisplayName, false);

            InstallMenuItem.Activated -= Install;
            InstallMenuItem.Action = null;
            UninstallMenuItem.Activated += Uninstall;

            await OpenItemAsync(domainIdentifier, notificationItemSettings.MountUrl, notificationItemSettings.DocumentUrl);

        }

        private async Task OpenItemAsync(string domainIdentifier, string webDavRootUrl, string webDavItemUrl)
        {
            NSFileProviderManager fileProviderManager = NSFileProviderManager.FromDomain(await Common.Core.Registrar.GetDomainAsync(domainIdentifier));
            string itemPath = (await fileProviderManager.GetUserVisibleUrlAsync(NSFileProviderItemIdentifier.RootContainer)).ToString() + "/" +
                                                                                webDavItemUrl.Substring(webDavRootUrl.Length);
            Logger.LogDebug($"Root domain path {await fileProviderManager.GetUserVisibleUrlAsync(NSFileProviderItemIdentifier.RootContainer)}");
            Logger.LogDebug($"File path {itemPath}");

            Process.Start("open", itemPath);
        }

        private async Task CheckDomainAndOpenItemAsync(string domainIdentifier, string webDavRootUrl, string webDavItemUrl)
        {
            string currentDomainIdentifier = await SecureStorage.GetAsync("CurrentDomainIdentifier");

            if (currentDomainIdentifier != domainIdentifier)
            {
                NSDistributedNotificationCenter.DefaultCenter.PostNotificationName("AnotherDomainIsRegistered",
                    JsonSerializer.Serialize(new NotificationAnotherDomainIsRegistered() {
                        DocumentUrl = webDavItemUrl,
                        MountUrl = webDavRootUrl,
                        CurrentDomain = currentDomainIdentifier
                    }));
            }
            else
            {
                await OpenItemAsync(domainIdentifier, webDavRootUrl, webDavItemUrl);
            }
        }
        

        private async Task<bool> CreateDomainAsync(string domainIdentifier, string domainDisplayName, bool openWebDavUrl)
        {
            bool success = false;

            // Get domain settings.
            DomainSettings domainSettings = await SecureStorage.GetAsync<DomainSettings>(domainIdentifier);

            // Open WebDav url in browser.
            if (openWebDavUrl)
            {
                Process.Start("open", domainSettings.WebDAVServerUrl);
            }

            // Register domain.
            NSFileProviderDomain? domain = await Common.Core.Registrar.RegisterAsync(domainIdentifier, domainDisplayName, Logger);
            if (domain != null)
            {
                // Save domain identifier.
                await SecureStorage.SetAsync("CurrentDomainIdentifier", domainIdentifier);
                RemoteStorageMonitor = new RemoteStorageMonitor(domainSettings.WebDAVServerUrl, domainSettings.WebSocketServerUrl, NSFileProviderManager.FromDomain(domain),
                                                                new ConsoleLogger(typeof(RemoteStorageMonitor).Name));
                RemoteStorageMonitor.ServerNotifications = new ServerNotifications(NSFileProviderManager.FromDomain(domain), RemoteStorageMonitor.Logger);
                await RemoteStorageMonitor.StartAsync();
                success = true;
            }

            return success;
        }
    }
}
