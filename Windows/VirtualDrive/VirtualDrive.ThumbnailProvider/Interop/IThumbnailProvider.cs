using System;
using System.Runtime.InteropServices;

namespace VirtualDrive.ThumbnailProvider.Interop
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("e357fccd-a995-4576-b01f-234630154e96")]
    public interface IThumbnailProvider
    {
        [PreserveSig]
        int GetThumbnail(UInt32 cx, out IntPtr phbmp, out WTS_ALPHATYPE pdwAlpha);
    }
}
