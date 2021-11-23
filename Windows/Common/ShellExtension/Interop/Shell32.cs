using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Interop
{
    public static class Shell32
    {
        public static Guid BHID_ThumbnailHandler = new Guid("7b2e650a-8e20-4f4a-b09e-6597afc72fb0");

        /// <summary>
        /// Creates and initializes a Shell item object from a parsing name.
        /// </summary>
        /// <param name="pszPath">A pointer to a display name.</param>
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern uint SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IBindCtx pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IShellItem ppv);
    }
}
