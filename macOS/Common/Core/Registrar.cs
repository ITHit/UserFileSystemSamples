using System;
using FileProvider;
using ITHit.FileSystem;

namespace Common.Core
{
    public static class Registrar
    {
        public static async Task<NSFileProviderDomain?> RegisterAsync(string extensionIdentifier, string displayName, ILogger logger)
        {            
            NSFileProviderDomain domain = new NSFileProviderDomain(extensionIdentifier, displayName);
            if (await IsRegisteredAsync(extensionIdentifier))
            {
                logger.LogMessage("File provider extension already registered", extensionIdentifier);
            }
            else
            {
                await NSFileProviderManager.AddDomainAsync(domain);
                logger.LogMessage("File provider extension registared succesefully", extensionIdentifier);
            }
            return domain;
        }

        public static async Task UnregisterAsync(string extensionIdentifier, ILogger logger)
        {
            NSFileProviderDomain domain = await GetDomainAsync(extensionIdentifier);
            if (!(await IsRegisteredAsync(extensionIdentifier)))
            {
                logger.LogMessage("File provider extension not found", extensionIdentifier);
                return;
            }

            await NSFileProviderManager.RemoveDomainAsync(domain);
            logger.LogMessage("File provider extension unregistared succesefully", extensionIdentifier);
        }

        public static async Task<bool> IsRegisteredAsync(string extensionIdentifier)
        {
            NSFileProviderDomain domain = await GetDomainAsync(extensionIdentifier);
            return domain != null;
        }

        public static async Task<NSFileProviderDomain> GetDomainAsync(string extensionIdentifier)
        {
            NSFileProviderDomain[] domains = await NSFileProviderManager.GetDomainsAsync();
            foreach (NSFileProviderDomain domain in domains)
            {
                if (domain.Identifier == extensionIdentifier)
                {
                    return domain;
                }
            }
            return null;
        }
    }
}

