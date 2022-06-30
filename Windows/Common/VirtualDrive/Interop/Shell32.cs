using System.Runtime.InteropServices;

namespace ITHit.FileSystem.Samples.Common.Windows.VirtualDrive.Interop
{
    internal static class Shell32
    {
        /// <summary>
        /// Notifies the system of an event that an application has performed. An application should use this function if it performs an action that may affect the Shell.
        /// </summary>
        /// <param name="wEventId">Describes the event that has occurred. Typically, only one event is specified at a time. If more than one event is specified, the
        /// values contained in the dwItem1 and dwItem2 parameters must be the same, respectively, for all specified events.</param>
        /// <param name="uFlags">Flags that, when combined bitwise with SHCNF_TYPE, indicate the meaning of the dwItem1 and dwItem2 parameters.</param>
        /// <param name="dwItem1">Optional. First event-dependent value.</param>
        /// <param name="dwItem2">Optional. Second event-dependent value.</param>
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern void SHChangeNotify(
            SHCNE wEventId,
            SHCNF uFlags,
            [MarshalAs(UnmanagedType.LPWStr)] string dwItem1,
            [Optional, MarshalAs(UnmanagedType.LPWStr)] string dwItem2);
    }
}
