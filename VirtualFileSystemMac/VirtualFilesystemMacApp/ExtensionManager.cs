using FileProvider;
using Foundation;

namespace VirtualFilesystemMacApp
{
    public class ExtensionManager
    {
        private NSFileProviderDomain Domain;
        private NSFileProviderManager ManagerForDomain;

        public ExtensionManager(string identifier, string displayName)
        {
            Domain = new NSFileProviderDomain(identifier, displayName);
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
                    if (domain.IsEqual(Domain))
                    {
                        // already installed
                        return;
                    }
                }

                NSFileProviderManager.AddDomain(Domain, (NSError error) =>
                {
                    if (error is not null)
                    {
                        // error installing
                        return;
                    }

                    ManagerForDomain = NSFileProviderManager.FromDomain(Domain);
                    if (ManagerForDomain is null)
                    {
                        // error creating manager
                        return;
                    }
                });
            });
        }

        public void UninstallExtension()
        {
            ManagerForDomain = null;
            NSFileProviderManager.RemoveDomain(Domain, (NSError error) =>
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
            return (ManagerForDomain is not null);
        }
    }
}
