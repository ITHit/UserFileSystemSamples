using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common.Windows.ShellExtension
{
    /// <summary>
    /// Implements Windows Explorer context menu logic.
    /// </summary>
    [ComVisible(true)]
    [ProgId("WebDAVDrive.ContextMenusProvider"), Guid(ContextMenusClass)]
    public class ContextMenusProvider : ContextMenusProviderCommon
    {
        public const string ContextMenusClass = "A22EBD03-343E-433C-98DF-372C6B3A1538";
        public static readonly Guid ContextMenusClassGuid = Guid.Parse(ContextMenusClass);
    }
}
