using System.Runtime.InteropServices;

using ITHit.FileSystem.Windows.ShellExtension;
using WebDAVDrive.Services;


namespace WebDAVDrive.ShellExtension
{
    /// <summary>
    /// Implements Windows Explorer Compare context menu.
    /// </summary>
    [ComVisible(true)]
    [ProgId("WebDAVDrive.ContextMenuVerbCompare")]
    [Guid("B05772FC-7A9E-41A7-A9FD-7917C616F273")]
    public class ContextMenuVerbIntegratedCompare : CloudFilesContextMenuVerbIntegratedBase
    {
        public ContextMenuVerbIntegratedCompare() : base(ServiceProvider.GetService<IDrivesService>().GetEngineWindowsDictionary())
        {
        }
    }
}
