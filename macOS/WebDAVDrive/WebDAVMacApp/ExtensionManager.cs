using System.Threading.Tasks;
using FileProvider;
using Foundation;

namespace WebDAVMacApp
{
    public class ExtensionManager
    {
        private NSFileProviderDomain fileProviderDomain;
        private NSFileProviderManager fileProviderManager;
        private RemoteStorageMonitor remoteStorageMonitor;

        public ExtensionManager(string identifier, string displayName)
        {
            fileProviderDomain = new NSFileProviderDomain(identifier, displayName);
        }

        public void InstallExtension()
        {
            NSFileProviderManager.GetDomains((domains, error) =>
            {
                if (error is not null)
                {
                    // error
                    return;
                }

                foreach (NSFileProviderDomain domain in domains)
                {
                    if (domain.IsEqual(fileProviderDomain))
                    {
                        // start remote monitor.
                        StartRemoteStorageMonitor();

                        // already installed
                        return;
                    }
                }

                NSFileProviderManager.AddDomain(fileProviderDomain, (NSError error) =>
                {
                    if (error is not null)
                    {
                        // error installing
                        return;
                    }

                    fileProviderManager = NSFileProviderManager.FromDomain(fileProviderDomain);
                    if (fileProviderManager is null)
                    {
                        // error creating manager
                        return;
                    }
                    else
                    {
                        // start remote monitor.
                        StartRemoteStorageMonitor();
                    }
                });
            });
        }

        public void UninstallExtension()
        {
            fileProviderManager = null;

            // start remote monitor.
            if(remoteStorageMonitor != null)
            {
                Task.Run(async () => await remoteStorageMonitor.StopAsync());
            }

            NSFileProviderManager.RemoveDomain(fileProviderDomain, (NSError error) =>
            {
                if (error is not null)
                {
                    // error uninstalling
                    return;
                }
            });
        }

        public bool IsExtensionInstalled()
        {
            return fileProviderManager != null;
        }

        /// <summary>
        /// Start remote storage monitor.
        /// </summary>
        private void StartRemoteStorageMonitor()
        {
            if (remoteStorageMonitor == null)
            {
                remoteStorageMonitor = new RemoteStorageMonitor(fileProviderDomain);
            }
            Task.Run(async () => await remoteStorageMonitor.StartAsync());
        }
    }
}
