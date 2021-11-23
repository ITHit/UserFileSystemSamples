using System;
using System.Runtime.InteropServices;

namespace ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Interop
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("7F73BE3F-FB79-493C-A6C7-7EE14E245841")]
    public interface IInitializedWithItem
    {
        [PreserveSig]
        int Initialize(IShellItem shellItem, STGM accessMode);
    }
}
