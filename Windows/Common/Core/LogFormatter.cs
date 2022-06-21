using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

using log4net;
using log4net.Config;
using log4net.Appender;

using ITHit.FileSystem.Windows;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Outputs logging.
    /// </summary>
    public class LogFormatter
    {
        /// <summary>
        /// Log file path.
        /// </summary>
        public readonly string LogFilePath;

        /// <summary>
        /// Indicates if more debugging and performance information should be logged.
        /// </summary>
        public bool DebugLoggingEnabled
        {
            get { return debugLoggingEnabled; }
            set
            {
                debugLoggingEnabled = value;
                string debugLoggingState = debugLoggingEnabled ? "Enabled" : "Disabled";
                log.Info($"{Environment.NewLine}Debug logging {debugLoggingState}");
            }
        }

        private bool debugLoggingEnabled = false;

        private readonly ILog log;

        private readonly string appId;

        private readonly string remoteStorageRootPath;

        private const int sourcePathWidth = 60;
        private const int remoteStorageIdWidth = 20;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="log">Log4net logger.</param>
        public LogFormatter(ILog log, string appId, string remoteStorageRootPath)
        {
            this.log = log;
            this.appId = appId;
            this.remoteStorageRootPath = remoteStorageRootPath;
            LogFilePath = ConfigureLogger();
        }

        /// <summary>
        /// Configures log4net logger.
        /// </summary>
        /// <returns>Log file path.</returns>
        private string ConfigureLogger()
        {
            // Load Log4Net for net configuration.
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "log4net.config")));

            // Update log file path for msix package. 
            RollingFileAppender rollingFileAppender = logRepository.GetAppenders().Where(p => p.GetType() == typeof(RollingFileAppender)).FirstOrDefault() as RollingFileAppender;
            if (rollingFileAppender != null && rollingFileAppender.File.Contains("WindowsApps"))
            {
                rollingFileAppender.File = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), appId,
                                                        Path.GetFileName(rollingFileAppender.File));
            }
            return rollingFileAppender?.File;
        }

        /// <summary>
        /// Prints environment description.
        /// </summary>
        public void PrintEnvironmentDescription()
        {
            // Log environment description.
            log.Info($"\n{"AppID:",-15} {appId}");
            log.Info($"\n{"Engine version:",-15} {typeof(IEngine).Assembly.GetName().Version}");
            log.Info($"\n{"OS version:",-15} {RuntimeInformation.OSDescription}");
            log.Info($"\n{".NET version:",-15} {RuntimeInformation.FrameworkDescription} {IntPtr.Size * 8}bit.");
            //log.Info($"\n{"Is UWP:",-15} {PackageRegistrar.IsRunningAsUwp()}");
            log.Info($"\n{"Admin mode:",-15} {new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator)}");
        }

        /// <summary>
        /// Print Engine config, settings, console commands, logging headers. 
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        /// <param name="remoteStorageRootPath">Remote storage root path.</param>
        public async Task PrintEngineStartInfoAsync(EngineWindows engine)
        {
            await PrintEngineEnvironmentDescriptionAsync(engine);

            // Log console commands.
            PrintHelp();

            // Log logging columns headers.
            PrintHeader();
        }

        public async Task PrintEngineEnvironmentDescriptionAsync(EngineWindows engine)
        {
            log.Info($"\n{"FS root:",-15} {engine.Path}");
            log.Info($"\n{"RS root:",-15} {remoteStorageRootPath}");
            log.Info($"\n{"AutoLock:",-15} {engine.AutoLock}");

            // Log indexing state. Sync root must be indexed.
            await PrintIndexingStateAsync(engine.Path);
        }

        /// <summary>
        /// Prints indexing state.
        /// </summary>
        /// <param name="path">File system path.</param>
        private async Task PrintIndexingStateAsync(string path)
        {
            StorageFolder userFileSystemRootFolder = await StorageFolder.GetFolderFromPathAsync(path);
            log.Info($"\n{"Indexed state:",-15} {await userFileSystemRootFolder.GetIndexedStateAsync()}");
        }

        /// <summary>
        /// Prints console commands.
        /// </summary>
        public void PrintHelp()
        {
            log.Info("\n\nPress Esc to unregister file system, delete all files/folders and exit (simulate uninstall).");
            log.Info("\nPress Spacebar to exit without unregistering (simulate reboot).");
            log.Info("\nPress 'p' to unregister sparse package.");
            log.Info("\nPress 'e' to start/stop the Engine and all sync services.");
            log.Info("\nPress 's' to start/stop synchronization service.");
            log.Info("\nPress 'm' to start/stop remote storage monitor.");
            log.Info("\nPress 'd' to enable/disable debug and performance logging.");
            log.Info($"\nPress 'l' to open log file. ({LogFilePath})");
            log.Info($"\nPress 'b' to submit support tickets, report bugs, suggest features. (https://userfilesystem.com/support/)");
            log.Info("\n----------------------\n");
        }

        public void LogError(IEngine sender, EngineErrorEventArgs e)
        {
            WriteLog(sender, e, log4net.Core.Level.Error);
        }

        public void LogMessage(IEngine sender, EngineMessageEventArgs e)
        {
            WriteLog(sender, e, log4net.Core.Level.Info);
        }

        public void LogDebug(IEngine sender, EngineMessageEventArgs e)
        {
            WriteLog(sender, e, log4net.Core.Level.Debug);
        }

        /// <summary>
        /// Outputs log message.
        /// </summary>
        /// <param name="log">log4net</param>
        /// <param name="e">Message or error description.</param>
        /// <param name="level">Log level.</param>
        private void WriteLog(IEngine sender, EngineMessageEventArgs e, log4net.Core.Level level)
        {
            string att = FsPath.Exists(e.SourcePath) ? FsPath.GetAttString(e.SourcePath) : null;
            string process = null;
            byte? priorityHint = null;
            string fileId = null;
            string size = null;

            if (e.OperationContext != null)
            {
                process = System.IO.Path.GetFileName(e.OperationContext.ProcessInfo?.ImagePath);
                priorityHint = e.OperationContext.PriorityHint;
                fileId = (e.OperationContext as IWindowsOperationContext).FileId.ToString();
                size = FsPath.FormatBytes((e.OperationContext as IWindowsOperationContext).FileSize);
            }

            string sourcePath = e.SourcePath?.FitString(sourcePathWidth, 6);
            string targetPath = e.TargetPath?.FitString(sourcePathWidth, 6);

            // Trim sync root and remote storage root to reduce ammount of logging and improve logs readability
            //if (sender != null)
            //{
                //sourcePath = sourcePath?.Replace((sender as EngineWindows).Path, "<FS root>");
                //sourcePath = sourcePath?.Replace(remoteStorageRootPath, "<RS root>");

                //targetPath = targetPath?.Replace((sender as EngineWindows).Path, "<FS root>");
                //targetPath = targetPath?.Replace(remoteStorageRootPath, "<RS root>");
            //}

            string message = Format(DateTimeOffset.Now.ToString("hh:mm:ss.fff"), process, priorityHint?.ToString(), fileId, "", e.ComponentName, e.CallerLineNumber.ToString(), e.CallerMemberName, e.CallerFilePath, e.Message,  sourcePath, att, targetPath);

            if (level == log4net.Core.Level.Error)
            {
                Exception ex = ((EngineErrorEventArgs)e).Exception;
                if (ex != null)
                {
                    message += Environment.NewLine;
                    log.Error(message, ex);
                }
            }
            else if (level == log4net.Core.Level.Info)
            {
                log.Info(message);
            }
            else if (level == log4net.Core.Level.Debug && DebugLoggingEnabled)
            {
                log.Debug(message);
            }
            
        }

        private static string Format(string date, string process, string priorityHint, string fileId, string remoteStorageId, string componentName, string callerLineNumber, string callerMemberName, string callerFilePath, string message, string sourcePath, string attributes, string targetPath)
        {
            // {fileId,-18} | {remoteStorageId,-remoteStorageIdWidth}
            return $"{Environment.NewLine}|{date, -12}| {process,-25}| {priorityHint,-5}| {componentName,-26}| {callerLineNumber, 4} | {message,-45}| {sourcePath,-sourcePathWidth} | {attributes, 23 } | {targetPath}";
        }

        /// <summary>
        /// Prints logging data headers.
        /// </summary>
        private void PrintHeader()
        {
            log.Info(Format("Time", "Process Name", "Prty", "FS ID", "RS ID", "Component", "Line", "Caller Member Name", "Caller File Path", "Message", "Source Path", "Attributes", "Target Path"));
            log.Info(Format("----", "------------", "----", "_____", "_____", "---------", "____", "------------------", "----------------", "-------", "-----------", "----------", "-----------"));
        }
    }

    static class StringExtensions
    {
        private const string replacement = "...";
        internal static string FitString(this string str, int maxLength, int startReplace)
        {
            int length = str.Length;
            if (length > maxLength)
            {
                string start = str.Substring(0, startReplace);
                string end = str.Substring(length - (maxLength - (startReplace + replacement.Length)));
                return $"{start}{replacement}{end}";
            }
            return str;
        }
    }

}
