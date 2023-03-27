using ITHit.FileSystem.Windows.ShellExtension;
using System.Runtime.InteropServices;


namespace VirtualDrive.ShellExtension
{
    
    /// <summary>
    /// Implements Windows Explorer context menu. Runs in one process with Engine.
    /// </summary>
    [ComVisible(true)]
    [ProgId("VirtualDrive.ContextMenuVerb")]
    [Guid("9C923BF3-3A4B-487B-AB4E-B4CF87FD1C25")]
    public class ContextMenuVerbIntegrated : CloudFilesContextMenuVerbIntegratedBase
    {
        public ContextMenuVerbIntegrated() : base(Program.Engine)
        {
        }
    }
    
}
