using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using CoreFoundation;
using Foundation;

using ITHit.FileSystem;

namespace Common.Core
{
    public static class NSLogHelper
    {
        // NSLog(NSString *format, ...) is a C variadic function. Passing a
        // separate vararg (e.g. "%s" + value) breaks on ARM64 (devices), where
        // variadic args are passed on the stack rather than in registers, which
        // a fixed-signature P/Invoke cannot satisfy. Instead we format the whole
        // message in managed code and pass it as the format itself with no
        // varargs (escaping '%'), which is ABI-safe on both ARM64 and x86_64.
        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation", EntryPoint = "NSLog")]
        extern static void NSLogInternal(IntPtr format);

        public static void NSLog(string format, params object[] args)
        {
            var val = (args is null || args.Length == 0) ? format : string.Format(format, args);

            // No variadic args are passed, so escape '%' to avoid format parsing.
            var fmt = CFString.CreateNative(val.Replace("%", "%%"));
            NSLogInternal(fmt);
            NSString.ReleaseNative(fmt);
        }
    }

    public class ConsoleLogger: ILogger
    {        
        private const string appName = "[ITHit.Filesystem.Mac]";

        public string ComponentName { get; }

        public ConsoleLogger(string componentName)
        {
            this.ComponentName = componentName;
        }

        public void LogError(IEngine sender, EngineErrorEventArgs e)
        {
            LogError(e.Message, sourcePath: e.SourcePath, targetPath: e.TargetPath, ex: e.Exception, operationContext: e.OperationContext, callerLineNumber: e.CallerLineNumber,
               callerMemberName: e.CallerMemberName, callerFilePath: e.CallerFilePath);
        }

        public void LogMessage(IEngine sender, EngineMessageEventArgs e)
        {
            LogMessage(e.Message, sourcePath: e.SourcePath, targetPath: e.TargetPath, operationContext: e.OperationContext, callerLineNumber: e.CallerLineNumber,
                callerMemberName: e.CallerMemberName, callerFilePath: e.CallerFilePath);
        }

        public void LogDebug(IEngine sender, EngineMessageEventArgs e)
        {
            LogDebug(e.Message, sourcePath: e.SourcePath, targetPath: e.TargetPath, operationContext: e.OperationContext, callerLineNumber: e.CallerLineNumber,
                callerMemberName: e.CallerMemberName, callerFilePath: e.CallerFilePath);
        }

        public void LogError(string message, string sourcePath = null, string targetPath = null, Exception ex = null, IOperationContext operationContext = null, IMetadata metadata = null, [CallerLineNumber] int callerLineNumber = 0, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null)
        {
            LogError($"\n{DateTimeOffset.Now} [{Thread.CurrentThread.ManagedThreadId,2}] {ComponentName,-26}{message,-45} {sourcePath,-80}", ex);
        }

        public void LogMessage(string message, string sourcePath = null, string targetPath = null, IOperationContext operationContext = null, IMetadata metadata = null, [CallerLineNumber] int callerLineNumber = 0, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null)
        {
            LogWithLevel(string.Empty, $"\n{ComponentName,-26}{message,-45} {sourcePath,-80} {targetPath}");
        }

        private void LogError(string str, Exception ex)
        {
            if(ex != null)
            {
                str += $"\nException Message:{ex?.Message}\nException Stack Trace:{ex?.StackTrace}";
            }

            LogWithLevel("[ERROR]", str);
        }

        private void LogWithLevel(string logLevelStr, string str)
        {
            NSLogHelper.NSLog((!string.IsNullOrEmpty(logLevelStr) ? (logLevelStr + " ") : string.Empty) + appName + " " + str);
        }

        public ILogger CreateLogger(string componentName)
        {
            return new ConsoleLogger(componentName);
        }

        public void LogDebug(string message, string sourcePath = null, string targetPath = null, IOperationContext operationContext = null, IMetadata metadata = null, [CallerLineNumber] int callerLineNumber = 0, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null)
        {
            LogDebug($"\n{ComponentName,-26}{message,-45} {sourcePath,-80} {targetPath}");
        }


        private void LogDebug(string str)
        {
#if DEBUG
            LogWithLevel("[DEBUG]", str);
#endif
        }        
    }
}
