using System;
using System.Runtime.InteropServices;

namespace VirtualDrive.ThumbnailProvider.Interop
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    public interface IShellItem
    {
        [PreserveSig]
        int BindToHandler(IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IntPtr ppv);
        int GetParent(out IShellItem ppsi);
        int GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        int Compare(IShellItem psi, uint hint, out int piOrder);
    }
}
