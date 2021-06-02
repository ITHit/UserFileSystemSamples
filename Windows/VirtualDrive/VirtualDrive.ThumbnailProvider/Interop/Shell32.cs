using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace VirtualDrive.ThumbnailProvider.Interop
{
    public static class Shell32
    {
        public static Guid BHID_ThumbnailHandler = new Guid("7b2e650a-8e20-4f4a-b09e-6597afc72fb0");

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IBindCtx pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IShellItem ppv);
    }
}
