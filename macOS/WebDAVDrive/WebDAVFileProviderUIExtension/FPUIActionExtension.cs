using Common.Core;
using FileProvider;
using ITHit.FileSystem.Mac;
using WebDAVCommon;
using WebDAVFileProviderUIExtension.ViewControllers;

namespace WebDAVFileProviderUIExtension;

[Register("FPUIActionExtension")]
public class FPUIActionExtension : FPUIActionExtensionViewControllerMac
{
    public FPUIActionExtension() : base(new ConsoleLogger(typeof(FPUIActionExtension).Name))
    { }

    /// <inheritdoc/>
    public override async Task<NSViewController> GetMenuCommandAsync(Guid menuGuid, IEnumerable<byte[]> remoteStorageItemIds, IMacFPUIActionExtensionContext context)
    {
        Logger.LogMessage($"{nameof(FPUIActionExtension)}.{nameof(GetMenuCommandAsync)}()");

        return new ContectMenuUIViewController(context);
    }

    /// <inheritdoc/>
    public override async Task<NSViewController> RequireAuthenticationAsync(string domainIdentifier, IMacFPUIActionExtensionContext context)
    {
        SecureStorage secureStorage = new SecureStorage(domainIdentifier);
        Logger.LogMessage($"{nameof(FPUIActionExtension)}.{nameof(RequireAuthenticationAsync)}()");  

        if (await secureStorage.GetAsync("LoginType") == "UserNamePassword")
        {
            return new AuthViewController(domainIdentifier, context);
        }
        else
        {
            Logger.LogMessage($"Return CookiesAuthViewController.");
            string url = await secureStorage.GetAsync("CookiesFailedUrl");
            Logger.LogMessage($"Return CookiesAuthViewController read url {url}.");
            return new CookiesAuthViewController(domainIdentifier, context, url);
        }
    }
}
