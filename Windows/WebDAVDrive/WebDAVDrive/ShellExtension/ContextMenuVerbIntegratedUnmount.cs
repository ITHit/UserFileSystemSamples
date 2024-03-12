using System.Runtime.InteropServices;

using ITHit.FileSystem.Windows.ShellExtension;


namespace WebDAVDrive.ShellExtension
{
    /// <summary>
    /// Implements Windows Explorer Unmount context menu, displayed on a root node.
    /// </summary>
    [ComVisible(true)]
    [ProgId("WebDAVDrive.ContextMenuVerbUnmount")]
    [Guid("FF039488-137F-454D-A546-AA329A1D963F")]
    public class ContextMenuVerbIntegratedUnmount : CloudFilesContextMenuVerbIntegratedBase
    {
        public ContextMenuVerbIntegratedUnmount() : base(Program.Engines)
        {
        }
    }
}
