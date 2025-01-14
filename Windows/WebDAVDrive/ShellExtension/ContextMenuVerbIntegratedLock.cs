using System.Runtime.InteropServices;

using ITHit.FileSystem.Windows.ShellExtension;
using WebDAVDrive.Services;


namespace WebDAVDrive.ShellExtension
{
    /// <summary>
    /// Implements Windows Explorer Lock context menu.
    /// </summary>
    [ComVisible(true)]
    [ProgId("WebDAVDrive.ContextMenuVerb")]
    [Guid("52140CC5-F5DC-4AAB-8AAD-82387C881319")]
    public class ContextMenuVerbIntegratedLock : CloudFilesContextMenuVerbIntegratedBase
    {
        public ContextMenuVerbIntegratedLock() : base(ServiceProvider.GetService<IDrivesService>().GetEngineWindowsDictionary())
        {
        }
    }
}
