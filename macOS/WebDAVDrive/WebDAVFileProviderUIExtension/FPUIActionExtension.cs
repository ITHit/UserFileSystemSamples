using Common.Core;
using FileProvider;
using ITHit.FileSystem.Mac;
using WebDAVCommon;
using WebDAVFileProviderUIExtension.ViewControllers;

namespace WebDAVFileProviderUIExtension;

[Register("FPUIActionExtension")]
public class FPUIActionExtension : FPUIActionExtensionViewControllerMac
{
    public FPUIActionExtension() : base(NSFileProviderManager.FromDomain(new NSFileProviderDomain(SecureStorage.ExtensionIdentifier, SecureStorage.ExtensionDisplayName)),
        new ConsoleLogger(typeof(FPUIActionExtension).Name))
    { }

    /// <inheritdoc/>
    public override async Task<NSViewController> GetMenuCommandAsync(Guid menuGuid, IEnumerable<byte[]> remoteStorageItemIds, IMacFPUIActionExtensionContext context)
    {
        Logger.LogMessage($"{nameof(FPUIActionExtension)}.{nameof(GetMenuCommandAsync)}()");

        return new ContectMenuUIViewController(context);
    }

    /// <inheritdoc/>
    public override async Task<NSViewController> RequireAuthenticationAsync(IMacFPUIActionExtensionContext context)
    {
        Logger.LogMessage($"{nameof(FPUIActionExtension)}.{nameof(RequireAuthenticationAsync)}()");

        return new AuthViewController(context);
    }
}
