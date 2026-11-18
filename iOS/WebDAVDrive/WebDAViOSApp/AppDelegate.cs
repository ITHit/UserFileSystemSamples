using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Core;
using FileProvider;
using Foundation;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;
using UIKit;
using WebDAVCommon;
using WebDAVMacApp;

namespace WebDAViOSApp
{
    [Register("AppDelegate")]
    public class AppDelegate : UIApplicationDelegate
    {
        private ILogger Logger = new ConsoleLogger("WebDavFileProviderHostApp");
        internal SecureStorage SecureStorage = new SecureStorage();
        internal Dictionary<string, RemoteStorageMonitor> DomainsRemoteStorageMonitor = new Dictionary<string, RemoteStorageMonitor>();
        internal List<string> ServerUrls = new List<string>();
        private readonly SemaphoreSlim monitorsLock = new SemaphoreSlim(1, 1);

        public override UIWindow? Window { get; set; }

        public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
            Logger.LogMessage("Starting FinishedLaunching");

            ServerUrls = Task.Run(async () => await SecureStorage.GetAsync<List<string>>("WebDAVServerURLs")).Result;
            if (ServerUrls == null || ServerUrls.Count == 0)
            {
                ServerUrls = AppGroupSettings.Settings.Value.WebDAVServerURLs;

                // Save urls to user's settings.
                Task.Run(async () => await SecureStorage.SetAsync("WebDAVServerURLs", ServerUrls)).Wait();
            }

            // Start monitors in background so launch is never blocked by network or File Provider calls.
            _ = Task.Run(StartRemoteStorageMonitorsAsync);

            // On iOS 13+ the window is created by the SceneDelegate; on earlier versions create it here.
            if (Window != null)
            {
                Window.RootViewController = new UINavigationController(new DriveListViewController(this, ServerUrls));
                Window.MakeKeyAndVisible();
            }

            Logger.LogMessage("Finished FinishedLaunching");
            return true;
        }

        /// <summary>
        /// Registers the File Provider domain and starts the remote storage monitor.
        /// </summary>
        internal async Task<bool> MountAsync(string webDAVServerUrl)
        {
            string domainIdentifier = new Uri(webDAVServerUrl).Host;

            // Save domain's WebDav server url.
            await SecureStorage.SetAsync(domainIdentifier, webDAVServerUrl);

            List<string> webDAVServerURLs = (await SecureStorage.GetAsync<List<string>>("WebDAVServerURLs")) ?? new List<string>();
            if (!webDAVServerURLs.Contains(webDAVServerUrl))
            {
                webDAVServerURLs.Add(webDAVServerUrl);
                await SecureStorage.SetAsync("WebDAVServerURLs", webDAVServerURLs);
            }

            NSFileProviderDomain? domain = await Common.Core.Registrar.RegisterAsync(domainIdentifier, domainIdentifier, Logger);
            if (domain == null)
            {
                return false;
            }

            await StartRemoteStorageMonitorAsync(domain, webDAVServerUrl);
            return true;
        }

        /// <summary>
        /// Stops the remote storage monitor and unregisters the File Provider domain.
        /// </summary>
        internal async Task UnmountAsync(string webDAVServerUrl)
        {
            string domainIdentifier = new Uri(webDAVServerUrl).Host;

            if (DomainsRemoteStorageMonitor.TryGetValue(domainIdentifier, out RemoteStorageMonitor monitor))
            {
                await monitor.StopAsync();
                monitor.Dispose();
                DomainsRemoteStorageMonitor.Remove(domainIdentifier);
            }

            await Common.Core.Registrar.UnregisterAsync(domainIdentifier, Logger);
        }

        /// <summary>
        /// Checks whether the File Provider domain is registered.
        /// </summary>
        internal Task<bool> IsMountedAsync(string webDAVServerUrl)
        {
            string domainIdentifier = new Uri(webDAVServerUrl).Host;
            return Common.Core.Registrar.IsRegisteredAsync(domainIdentifier);
        }

        /// <summary>
        /// Starts (or reconnects) monitors for all registered domains. Safe to call repeatedly.
        /// </summary>
        private async Task StartRemoteStorageMonitorsAsync()
        {
            foreach (string serverUrl in ServerUrls.ToArray())
            {
                try
                {
                    string domainIdentifier = new Uri(serverUrl).Host;
                    if (await Common.Core.Registrar.IsRegisteredAsync(domainIdentifier))
                    {
                        await StartRemoteStorageMonitorAsync(await Common.Core.Registrar.GetDomainAsync(domainIdentifier), serverUrl);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to start remote storage monitor.", serverUrl, null, ex);
                }
            }
        }

        private async Task StartRemoteStorageMonitorAsync(NSFileProviderDomain domain, string webDAVServerUrl)
        {
            await monitorsLock.WaitAsync();
            try
            {
                if (DomainsRemoteStorageMonitor.TryGetValue(domain.Identifier, out RemoteStorageMonitor? existing))
                {
                    // Already listening, nothing to do. Otherwise reconnect (iOS drops sockets while suspended).
                    if (existing.SyncState != SynchronizationState.Enabled)
                    {
                        await existing.StopAsync();
                        await existing.StartAsync();
                    }
                }
                else
                {
                    RemoteStorageMonitor remoteStorageMonitor = new RemoteStorageMonitor(domain.Identifier, webDAVServerUrl,
                        $"ws{(webDAVServerUrl.StartsWith("https:") ? "s" : string.Empty)}://{domain.Identifier}",
                        NSFileProviderManager.FromDomain(domain), new ConsoleLogger(typeof(RemoteStorageMonitor).Name));
                    remoteStorageMonitor.ServerNotifications = new ServerNotifications(domain.Identifier, NSFileProviderManager.FromDomain(domain), remoteStorageMonitor.Logger);
                    await remoteStorageMonitor.StartAsync();
                    DomainsRemoteStorageMonitor.Add(domain.Identifier, remoteStorageMonitor);
                }
            }
            finally
            {
                monitorsLock.Release();
            }
        }

        // iOS suspends the app shortly after it leaves the foreground and kills its sockets,
        // so reconnect (which also pulls all missed changes) every time the app becomes active.
        // Called from SceneDelegate because UIApplicationDelegate lifecycle callbacks are not used with scenes.
        internal void OnAppActive()
        {
            _ = Task.Run(StartRemoteStorageMonitorsAsync);
        }

        // Ask for extra time so in-flight changes can finish before the app is suspended.
        internal void OnAppBackground(UIApplication application)
        {
            nint taskId = 0;
            taskId = application.BeginBackgroundTask("WebDAVMonitor", () => application.EndBackgroundTask(taskId));
            _ = Task.Delay(TimeSpan.FromSeconds(25)).ContinueWith(_ => application.EndBackgroundTask(taskId));
        }

        public override void WillTerminate(UIApplication application)
        {
            foreach (RemoteStorageMonitor remoteStorageMonitor in DomainsRemoteStorageMonitor.Values)
            {
                remoteStorageMonitor?.StopAsync();
                remoteStorageMonitor?.Dispose();
            }
        }
    }
}
