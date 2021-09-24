using System;
using System.Runtime.InteropServices;

namespace VirtualDrive.ShellExtension.Interop
{
    [ComImport]
    [Guid("A88826F8-186F-4987-AADE-EA0CEF8FBFE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEnumExplorerCommand
    {
        [PreserveSig]
        int Next(uint celt, 
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Interface, SizeParamIndex = 0)] 
            out IExplorerCommand[] pUICommand,
            out uint pceltFetched);

        [PreserveSig]
        int Skip(uint celt);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Clone(out IEnumExplorerCommand ppenum);
    }
}
