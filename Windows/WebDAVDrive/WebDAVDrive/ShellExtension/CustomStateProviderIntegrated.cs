using System.Runtime.InteropServices;

using ITHit.FileSystem.Windows.ShellExtension;
using WebDAVDrive.Services;
using WebDAVDrive.Utils;


namespace WebDAVDrive.ShellExtension
{
    /// <summary>
    /// Implements custom state provider for virtual drive.
    /// Displays custom colums and custom state icons in Status column in Windows Explorer.
    /// </summary>
    [ComVisible(true)]
    [ProgId("WebDAVDrive.CustomStateProvider")]
    [Guid("B88C1E80-D493-4EEE-B772-AAF34734B89B")]
    public class CustomStateProviderIntegrated : CustomStateHandlerIntegratedBase
    {
        public CustomStateProviderIntegrated() : base(ServiceProviderUtil.GetService<IDomainsService>().GetEngineWindowsDictionary())
        {
        }
    }
}
