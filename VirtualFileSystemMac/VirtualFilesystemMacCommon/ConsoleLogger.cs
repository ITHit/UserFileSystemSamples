using System;
using System.Runtime.InteropServices;
using Foundation;

namespace VirtualFilesystemMacCommon
{
    public static class NSLogHelper
    {
        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        extern static void NSLog(IntPtr format, [MarshalAs(UnmanagedType.LPStr)] string s);

        public static void NSLog(string format, params object[] args)
        {
            var fmt = NSString.CreateNative("%s");
            var val = (args is null || args.Length == 0) ? format : string.Format(format, args);

            NSLog(fmt, val);
            NSString.ReleaseNative(fmt);
        }
    }

    public class ConsoleLogger
    {
        private const string AppName = "[ITHit]";
        private readonly string ModuleName;

        public ConsoleLogger(string moduleName)
        {
            ModuleName = moduleName;
        }

        public void LogError(Exception exception)
        {
            LogError(exception.ToString());
        }

        public void LogError(string str)
        {
            LogWithLevel("[ERROR]", str);
        }

        public void LogDebug(Exception exception)
        {
#if DEBUG
            LogDebug(exception.ToString());
#endif
        }

        public void LogDebug(string str)
        {
#if DEBUG
            LogWithLevel("[DEBUG]", str);
#endif
        }

        private void LogWithLevel(string logLevelStr, string str)
        {
            NSLogHelper.NSLog(logLevelStr + " " + AppName + " " + ModuleName + ": " + str);
        }
    }
}
