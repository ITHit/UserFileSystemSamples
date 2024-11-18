using System.Runtime.InteropServices;

using ITHit.FileSystem.Windows.ShellExtension;
using WebDAVDrive.Services;
using WebDAVDrive.Utils;


namespace WebDAVDrive.ShellExtension
{
    /// <summary>
    /// Implements Windows Explorer Unmount context menu, displayed on a root node.
    /// </summary>
    [ComVisible(true)]
    [ProgId("WebDAVDrive.ContextMenuVerbUnmount")]
    [Guid("9FC5E094-5F3B-4417-995E-68ABF987CF66")]
    public class ContextMenuVerbIntegratedUnmount : CloudFilesContextMenuVerbIntegratedBase
    {
        public ContextMenuVerbIntegratedUnmount() : base(ServiceProviderUtil.GetService<IDomainsService>().GetEngineWindowsDictionary())
        {
        }
    }
}
