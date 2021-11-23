using System;
using System.Runtime.InteropServices;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension;

namespace VirtualDrive.ShellExtension
{
    /// <summary>
    /// Implements Windows Explorer context menu logic.
    /// </summary>
    [ComVisible(true)]
    [ProgId("VirtualDrive.ContextMenusProvider"), Guid(ContextMenusClass)]
    public class ContextMenusProvider : ContextMenusProviderCommon
    {
        public const string ContextMenusClass = "9C923BF3-3A4B-487B-AB4E-B4CF87FD1C25";
        public static readonly Guid ContextMenusClassGuid = Guid.Parse(ContextMenusClass);
    }
}
