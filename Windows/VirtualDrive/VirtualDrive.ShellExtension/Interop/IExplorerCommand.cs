using System;
using System.Runtime.InteropServices;

namespace VirtualDrive.ShellExtension.Interop
{
    [ComImport]
    [Guid("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IExplorerCommand
    {
        [PreserveSig]
        int GetTitle(
            IShellItemArray itemArray,
            [MarshalAs(UnmanagedType.LPWStr)] out string title);

        [PreserveSig]
        int GetIcon(IShellItemArray itemArray,
            [MarshalAs(UnmanagedType.LPWStr)] out string resourceString);

        [PreserveSig]
        int GetToolTip(IShellItemArray itemArray,
            [MarshalAs(UnmanagedType.LPWStr)] out string tooltip);

        [PreserveSig]
        int GetCanonicalName(out Guid guid);

        [PreserveSig]
        int GetState(IShellItemArray itemArray,
            [MarshalAs(UnmanagedType.Bool)] bool okToBeShow,
            out EXPCMDSTATE commandState);

        [PreserveSig]
        int Invoke(IShellItemArray itemArray,
            [MarshalAs(UnmanagedType.Interface)] object bindCtx);

        [PreserveSig]
        int GetFlags(out EXPCMDFLAGS flags);

        [PreserveSig]
        int EnumSubCommands(out IEnumExplorerCommand commandEnum);
    }
}
