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

        public bool debugLoggingEnabled = false;

        private readonly ILog log;

        private readonly string appId;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="log">Log4net logger.</param>
        public LogFormatter(ILog log, string appId)
        {
            this.log = log;
            this.appId = appId;
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
            log.Info($"\n{"Env version:",-15} {RuntimeInformation.FrameworkDescription} {IntPtr.Size * 8}bit.");
            //log.Info($"\n{"Is UWP:",-15} {PackageRegistrar.IsRunningAsUwp()}");
            log.Info($"\n{"Admin mode:",-15} {new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator)}"); 
        }

        /// <summary>
        /// Prints indexing state.
        /// </summary>
        /// <param name="path">File system path.</param>
        public async Task PrintIndexingStateAsync(string path)
        {
            StorageFolder userFileSystemRootFolder = await StorageFolder.GetFolderFromPathAsync(path);
            log.Info($"\nIndexed state: {(await userFileSystemRootFolder.GetIndexedStateAsync())}");
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
            log.Info("\nPress 's' to start/stop full synchronization service.");
            log.Info("\nPress 'm' to start/stop remote storage monitor.");
            log.Info("\nPress 'd' to enable/disable debug and performance logging.");
            log.Info($"\nPress 'l' to open log file. ({LogFilePath})");
            log.Info($"\nPress 'b' to submit support tickets, report bugs, suggest features. (https://userfilesystem.com/support/)");
            log.Info("\n----------------------\n");
        }

        public void LogError(IEngine sender, EngineErrorEventArgs e)
        {
            WriteLog(e, log4net.Core.Level.Error);
        }

        public void LogMessage(IEngine sender, EngineMessageEventArgs e)
        {
            WriteLog(e, log4net.Core.Level.Info);
        }

        public void LogDebug(IEngine sender, EngineMessageEventArgs e)
        {
            WriteLog(e, log4net.Core.Level.Debug);
        }

        /// <summary>
        /// Outputs log message.
        /// </summary>
        /// <param name="log">log4net</param>
        /// <param name="e">Message or error description.</param>
        /// <param name="level">Log level.</param>
        private void WriteLog(EngineMessageEventArgs e, log4net.Core.Level level)
        {
            string att = FsPath.Exists(e.SourcePath) ? FsPath.GetAttString(e.SourcePath) : null;
            string process = null;
            byte? priorityHint = null;
            ulong? clientFileId = null;
            string size = null;

            if (e.OperationContext != null)
            {
                process = System.IO.Path.GetFileName(e.OperationContext.ProcessInfo?.ImagePath);
                priorityHint = e.OperationContext.PriorityHint;
                clientFileId = (e.OperationContext as IWindowsOperationContext).FileId;
                size = FsPath.FormatBytes((e.OperationContext as IWindowsOperationContext).FileSize);
            }

            string message = Format(DateTimeOffset.Now.ToString("hh:mm:ss.fff"), process, priorityHint?.ToString(), e.ComponentName, e.Message, e.SourcePath, att, e.TargetPath);

            if (level == log4net.Core.Level.Error)
            {
                Exception ex = ((EngineErrorEventArgs)e).Exception;
                message += Environment.NewLine;
                log.Error(message, ex);
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

        private static string Format(string date, string process, string priorityHint, string componentName, string message, string sourcePath, string attributes, string targetPath)
        {
            return $"{Environment.NewLine}|{date, -12}| {process,-25}| {priorityHint,-5}| {componentName,-26}| {message,-45}| {sourcePath,-80}| {attributes, -22}| {targetPath}";
        }

        /// <summary>
        /// Prints logging data headers.
        /// </summary>
        public void PrintHeader()
        {
            log.Info(Format("Time", "Process Name", "Prty", "Component", "Operation", "Source Path", "Attributes", "Target Path"));
            log.Info(Format("----", "------------", "----", "---------", "---------", "-----------", "----------", "-----------"));
        }
    }
}
