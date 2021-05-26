using System;
using System.Runtime.InteropServices;
using System.Threading;
using Foundation;
using ITHit.FileSystem;

namespace FileProviderExtension
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

    public class ConsoleLogger: ILogger
    {        
        private const string appName = "[ITHit.Filesystem.Mac]";
        private readonly string componentName;

        public ConsoleLogger(string componentName)
        {
            this.componentName = componentName;
        }

        public void LogError(string message, string sourcePath = null, string targetPath = null, Exception ex = null)
        {
            LogError($"\n{DateTimeOffset.Now} [{Thread.CurrentThread.ManagedThreadId,2}] {componentName,-26}{message,-45} {sourcePath,-80}", ex);
        }

        public void LogMessage(string message, string sourcePath = null, string targetPath = null)
        {
            LogDebug($"\n{DateTimeOffset.Now} [{Thread.CurrentThread.ManagedThreadId,2}] {componentName,-26}{message,-45} {sourcePath,-80} {targetPath}");
        }

        private void LogError(string str, Exception ex)
        {
            if(ex != null)
            {
                str += $"\nException Message:{ex?.Message}\nException Stack Trace:{ex?.StackTrace}";
            }

            LogWithLevel("[ERROR]", str);
        }

        private void LogDebug(string str)
        {
#if DEBUG
            LogWithLevel("[DEBUG]", str);
#endif
        }

        private void LogWithLevel(string logLevelStr, string str)
        {
            NSLogHelper.NSLog(logLevelStr + " " + appName + " " + str);
        }
    }
}
