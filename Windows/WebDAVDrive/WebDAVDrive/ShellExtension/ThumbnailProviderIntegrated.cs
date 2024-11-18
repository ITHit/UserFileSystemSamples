using System.Runtime.InteropServices;

using ITHit.FileSystem.Windows.ShellExtension;
using WebDAVDrive.Services;
using WebDAVDrive.Utils;


namespace WebDAVDrive.ShellExtension
{
    /// <summary>
    /// Thumbnails provider Windows Shell Extension.
    /// </summary>
    [ComVisible(true)]
    [ProgId("WebDAVDrive.ThumbnailProvider")]
    [Guid("C3B06859-38F0-4F6D-AC06-39E1B2C0C77E")]
    public class ThumbnailProviderIntegrated : ThumbnailProviderHandlerIntegratedBase
    {
        public ThumbnailProviderIntegrated() : base(ServiceProviderUtil.GetService<IDomainsService>().GetEngineWindowsDictionary())
        {
        }
    }
}
