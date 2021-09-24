using System;
using System.Runtime.InteropServices;

namespace VirtualDrive.ShellExtension.Interop
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("70629033-E363-4A28-A567-0DB78006E6D7")]
    public interface IEnumShellItems
    {
        [PreserveSig]
        int Next(uint celt,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Interface, SizeParamIndex = 0)] out IShellItem[] rgelt,
            out uint pceltFetched);

        [PreserveSig]
        int Skip(uint celt);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Clone(out IEnumShellItems ppenum);
    }
}
