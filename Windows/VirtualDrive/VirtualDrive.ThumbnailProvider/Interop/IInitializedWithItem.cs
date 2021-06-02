using System;
using System.Runtime.InteropServices;

namespace VirtualDrive.ThumbnailProvider.Interop
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("7f73be3f-fb79-493c-a6c7-7ee14e245841")]
    public interface IInitializedWithItem
    {
        [PreserveSig]
        int Initialize(IShellItem shellItem, STGM accessMode);
    }
}
