using System;
using System.Runtime.InteropServices;

namespace VirtualDrive.ShellExtension.Interop
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("B63EA76D-1F85-456F-A19C-48159EFA858B")]
    public interface IShellItemArray
    {
        [PreserveSig]
        int BindToHandler(IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IntPtr ppv);

        [PreserveSig]
        int GetPropertyStore(
            int flags,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IntPtr ppv);

        [PreserveSig]
        int GetPropertyDescriptionList(
            int keyType,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IntPtr ppv);

        [PreserveSig]
        int GetAttributes(
            SIATTRIBFLAGS dwAttribFlags,
            uint sfgaoMask,
            out int psfgaoAttribs);

        [PreserveSig]
        int GetCount(out ushort pdwNumItems);

        [PreserveSig]
        int GetItemAt(
            ushort dwIndex,
            [MarshalAs(UnmanagedType.Interface)] 
            out IShellItem ppsi);

        [PreserveSig]
        int EnumItems(
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Interface, SizeParamIndex = 0)] 
            out IEnumShellItems[] ppenumShellItems);
    }
}
