using System.Runtime.InteropServices;

using ITHit.FileSystem.Windows.ShellExtension;


namespace WebDAVDrive.ShellExtension
{
    /// <summary>
    /// Implements Windows Explorer Compare context menu.
    /// </summary>
    [ComVisible(true)]
    [ProgId("WebDAVDrive.ContextMenuVerbCompare")]
    [Guid("A54BD1AD-4816-44B0-9247-8F43D8CA7AE7")]
    public class ContextMenuVerbIntegratedCompare : CloudFilesContextMenuVerbIntegratedBase
    {
        public ContextMenuVerbIntegratedCompare() : base(Program.Engine)
        {
        }
    }
}
