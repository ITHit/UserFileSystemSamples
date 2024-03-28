using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Web;
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
        private const string ProtocolPrefix = "fuse:";
        private ILogger Logger = new ConsoleLogger("WebDavFileProviderHostApp");
        private SecureStorage SecureStorage = new SecureStorage();

        private NSStatusItem StatusItem;
        private Dictionary<string, (NSMenuItem installMenu, NSMenuItem uninstallMenu)> DomainsMenuItems = new Dictionary<string, (NSMenuItem installMenu, NSMenuItem uninstallMenu)>();
        private Dictionary<string, RemoteStorageMonitor> DomainsRemoteStorageMonitor = new Dictionary<string, RemoteStorageMonitor>();

        public AppDelegate()
        {
        }

        public override void DidFinishLaunching(NSNotification notification)
        {
            Logger.LogMessage("Starting DidFinishLaunching");

            List<string> webDAVServerURLs = Task.Run<List<string>>(async () => await SecureStorage.GetAsync<List<string>>("WebDAVServerURLs")).Result;

            if (webDAVServerURLs == null)
            {
                webDAVServerURLs = AppGroupSettings.Settings.Value.WebDAVServerURLs;

                // Save urls to user's settings.
                Task.Run(async () => await SecureStorage.SetAsync("WebDAVServerURLs", webDAVServerURLs)).Wait();
            }

            StatusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(30);
            StatusItem.Menu = new NSMenu();
            StatusItem.Image = NSImage.ImageNamed("TrayIcon.png");
            StatusItem.HighlightMode = true;

            foreach (string serverUrl in webDAVServerURLs)
            {
                AddDomainMenu(serverUrl);
            }

            NSMenuItem exitMenuItem = new NSMenuItem("Quit", (a, b) =>
            {
                NSApplication.SharedApplication.Terminate(this);
            });
            StatusItem.Menu.AddItem(exitMenuItem);


            Logger.LogMessage("Finished DidFinishLaunching");
        }


        public override async void OpenUrls(NSApplication application, NSUrl[] urls)
        {
            Logger.LogMessage($"OpenUrls - {string.Join(",", urls.Select(p => p.AbsoluteUrl))}");

            foreach (NSUrl url in urls)
            {
                Dictionary<string, string> parameters = url.AbsoluteUrl.ToString().Replace(ProtocolPrefix, string.Empty).Split(';').ToDictionary(p => p.Split('=')[0], p => p.Split('=')[1]);

                Uri itemUrl = new Uri(HttpUtility.UrlDecode(parameters["ItemUrl"]));
                Uri webDAVServerUrl = new Uri(HttpUtility.UrlDecode(parameters["MountUrl"]));
                string domainIdentifier = webDAVServerUrl.Host;

                List<string> webDAVServerURLs = await SecureStorage.GetAsync<List<string>>("WebDAVServerURLs");
                Task<bool> taskIsExtensionRegistered = Task.Run<bool>(async () => await Common.Core.Registrar.IsRegisteredAsync(domainIdentifier));
                bool isExtensionRegistered = taskIsExtensionRegistered.Result;
                if (!isExtensionRegistered)
                {
                    if (!DomainsMenuItems.ContainsKey(domainIdentifier))
                    {
                        // Add new url.
                        webDAVServerURLs.Add(webDAVServerUrl.AbsoluteUri);


                        // Add menu item with domain name.
                        AddDomainMenu(webDAVServerUrl.AbsoluteUri);
                    }
                    else
                    {
                        await SecureStorage.SetAsync(domainIdentifier, webDAVServerUrl.AbsoluteUri);

                        // Update existing.url.
                        int index = webDAVServerURLs.FindIndex(p => p.Contains(domainIdentifier));
                        if (index != -1)
                        {
                            webDAVServerURLs[index] = webDAVServerUrl.AbsoluteUri;
                        }
                    }

                    // Save webdav server url to user's settings.  
                    await SecureStorage.SetAsync("WebDAVServerURLs", webDAVServerURLs);

                    // Register domain.
                    Install(DomainsMenuItems[domainIdentifier].installMenu, null, false);
                }
                else
                {
                    if (await SecureStorage.GetAsync(domainIdentifier) != webDAVServerUrl.AbsoluteUri)
                    {
                        Uninstall(DomainsMenuItems[domainIdentifier].uninstallMenu, null);

                        // update WebDAV server url.
                        await SecureStorage.SetAsync(domainIdentifier, webDAVServerUrl.AbsoluteUri);

                        // Update existing.url.
                        int index = webDAVServerURLs.FindIndex(p => p.Contains(domainIdentifier));
                        if(index != -1)
                        {
                            webDAVServerURLs[index] = webDAVServerUrl.AbsoluteUri;
                        }

                        await SecureStorage.SetAsync("WebDAVServerURLs", webDAVServerURLs);

                        Install(DomainsMenuItems[domainIdentifier].installMenu, null, false);
                    }
                }

                // Open item url.
                NSFileProviderManager fileProviderManager = NSFileProviderManager.FromDomain(await Common.Core.Registrar.GetDomainAsync(domainIdentifier));
                string itemPath = (await fileProviderManager.GetUserVisibleUrlAsync(NSFileProviderItemIdentifier.RootContainer)).ToString() + "/" +
                                                                                    itemUrl.AbsoluteUri.Substring(webDAVServerUrl.AbsoluteUri.Length);
                Process.Start("open", itemPath);
            }
        }

        private void AddDomainMenu(string serverUrl)
        {
            Uri webDavRootUri = new Uri(serverUrl);
            NSMenuItem domainMenu = new NSMenuItem(webDavRootUri.Host);
            NSMenuItem installMenuItem = new NSMenuItem("Install");
            NSMenuItem uninstallMenuItem = new NSMenuItem("Uninstall");
            string domainIdentifier = webDavRootUri.Host;

            // Save domain's WebDav server url.
            Task.Run(async () =>
            {
                await SecureStorage.SetAsync(domainIdentifier, serverUrl);
            }).Wait();

            Task<bool> taskIsExtensionRegistered = Task.Run<bool>(async () => await Common.Core.Registrar.IsRegisteredAsync(domainIdentifier));
            bool isExtensionRegistered = taskIsExtensionRegistered.Result;
            if (isExtensionRegistered)
            {
                Task.Run(async () => await StartRemoteStorageMonitorAsync(await Common.Core.Registrar.GetDomainAsync(domainIdentifier), serverUrl)).Wait();
                uninstallMenuItem.Activated += Uninstall;
            }
            else
            {
                installMenuItem.Activated += Install;
            }
            installMenuItem.Identifier = uninstallMenuItem.Identifier = webDavRootUri.Host;

            domainMenu.Submenu = new NSMenu();
            domainMenu.Submenu.AddItem(installMenuItem);
            domainMenu.Submenu.AddItem(uninstallMenuItem);
            StatusItem.Menu.InsertItem(domainMenu, 0);

            DomainsMenuItems.Add(webDavRootUri.Host, new(installMenuItem, uninstallMenuItem));
        }

        private void Install(object? sender, EventArgs e)
        {
            Install(sender, e, true);
        }

        private void Install(object? sender, EventArgs e, bool openWebDavUrl)
        {
            string domainIdentifier = (sender as NSMenuItem).Identifier;
            if (Task.Run<bool>(async () =>
            {
                return await RegisterDomainAsync(domainIdentifier, openWebDavUrl);
            }).Result)
            {
                DomainsMenuItems[domainIdentifier].installMenu.Activated -= Install;
                DomainsMenuItems[domainIdentifier].installMenu.Action = null;
                DomainsMenuItems[domainIdentifier].uninstallMenu.Activated += Uninstall;
            }
        }

        private void Uninstall(object? sender, EventArgs e)
        {
            string domainIdentifier = (sender as NSMenuItem).Identifier;

            Task.Run(async () =>
            {
                await DomainsRemoteStorageMonitor[domainIdentifier]?.StopAsync();
                await Common.Core.Registrar.UnregisterAsync(domainIdentifier, Logger);
            }).Wait();

            DomainsMenuItems[domainIdentifier].installMenu.Activated += Install;
            DomainsMenuItems[domainIdentifier].uninstallMenu.Activated -= Uninstall;
            DomainsMenuItems[domainIdentifier].uninstallMenu.Action = null;
        }

        public override void WillTerminate(NSNotification notification)
        {
            foreach (RemoteStorageMonitor remoteStorageMonitor in DomainsRemoteStorageMonitor.Values)
            {
                remoteStorageMonitor?.StopAsync();
                remoteStorageMonitor?.Dispose();
            }
        }

        private async Task<bool> RegisterDomainAsync(string domainIdentifier, bool openWebDavUrl = false)
        {
            bool success = false;

            string webDAVServerUrl = await SecureStorage.GetAsync(domainIdentifier);

            // Open WebDav url in browser.
            if (openWebDavUrl)
            {
                Process.Start("open", webDAVServerUrl);
            }

            // Register domain.
            NSFileProviderDomain? domain = await Common.Core.Registrar.RegisterAsync(domainIdentifier, domainIdentifier, Logger);
            if (domain != null)
            {
                await StartRemoteStorageMonitorAsync(domain, webDAVServerUrl);
                success = true;
            }

            return success;
        }

        private async Task StartRemoteStorageMonitorAsync(NSFileProviderDomain domain, string webDAVServerUrl)
        {
            if (DomainsRemoteStorageMonitor.ContainsKey(domain.Identifier))
            {
                DomainsRemoteStorageMonitor[domain.Identifier].StartAsync();
            }
            else
            {
                RemoteStorageMonitor remoteStorageMonitor = new RemoteStorageMonitor(webDAVServerUrl,
               $"ws{(webDAVServerUrl.StartsWith("https:") ? "s" : string.Empty)}://{domain.Identifier}",
               NSFileProviderManager.FromDomain(domain), new ConsoleLogger(typeof(RemoteStorageMonitor).Name));
                remoteStorageMonitor.ServerNotifications = new ServerNotifications(NSFileProviderManager.FromDomain(domain), remoteStorageMonitor.Logger);
                await remoteStorageMonitor.StartAsync();
                DomainsRemoteStorageMonitor.Add(domain.Identifier, remoteStorageMonitor);
            }
        }
    }
}
