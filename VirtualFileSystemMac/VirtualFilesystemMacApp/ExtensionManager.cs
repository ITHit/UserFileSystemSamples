using FileProvider;
using Foundation;
using VirtualFilesystemCommon;

namespace VirtualFilesystemMacApp
{
    public class ExtensionManager
    {
        private NSFileProviderDomain fileProviderDomain;
        private NSFileProviderManager fileProviderManager;

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
                });
            });
        }

        public void UninstallExtension()
        {
            fileProviderManager = null;
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
    }
}
