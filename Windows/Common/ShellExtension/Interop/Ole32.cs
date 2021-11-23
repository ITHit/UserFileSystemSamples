using System;
using System.Runtime.InteropServices;

namespace ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Interop
{
    public static class Ole32
    {
        public const int CLSCTX_LOCAL_SERVER = 0x4;

        public const int REGCLS_MULTIPLEUSE = 1;
        public const int REGCLS_SUSPENDED = 4;

        [DllImport(nameof(Ole32))]
        public static extern int CoRegisterClassObject(ref Guid guid, [MarshalAs(UnmanagedType.IUnknown)] object obj, int context, int flags, out int register);

        [DllImport(nameof(Ole32))]
        public static extern int CoResumeClassObjects();

        [DllImport(nameof(Ole32))]
        public static extern int CoRevokeClassObject(int register);
    }
}
