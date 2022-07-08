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
using ITHit.FileSystem.Windows.Package;

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
        /// Prints environment description and console commands.
        /// </summary>
        public void PrintEnvironmentDescription()
        {
            // Log environment description.
            log.Info($"\n{"AppID:",-25} {appId}");
            log.Info($"\n{"Engine version:",-25} {typeof(IEngine).Assembly.GetName().Version}");
            log.Info($"\n{"OS version:",-25} {RuntimeInformation.OSDescription}");
            log.Info($"\n{".NET version:",-25} {RuntimeInformation.FrameworkDescription} {IntPtr.Size * 8}bit.");
            log.Info($"\n{"Package or app identity:",-25} {PackageRegistrar.IsRunningWithIdentity()}");
            log.Info($"\n{"Sparse package identity:",-25} {PackageRegistrar.IsRunningWithSparsePackageIdentity()}");
            log.Info($"\n{"Elevated mode:",-25} {new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator)}");

            string sparsePackagePath = PackageRegistrar.GetSparsePackagePath();
            if (File.Exists(sparsePackagePath))
            {
                log.Info($"\n{"Sparse package location:",-25} {sparsePackagePath}");
                var cert = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(sparsePackagePath);
                log.Info($"\n{"Sparse package cert:",-25} Subject: {cert.Subject}, Issued by: {cert.Issuer}");
            }
            else
            {
                log.Info($"\n{"Sparse package:",-25} Not found");
            }

            // Log console commands.
            PrintHelp();
        }

        /// <summary>
        /// Print Engine config, settings, logging headers. 
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        /// <param name="remoteStorageRootPath">Remote storage root path.</param>
        public async Task PrintEngineStartInfoAsync(EngineWindows engine)
        {
            await PrintEngineEnvironmentDescriptionAsync(engine);
            log.Info("\n");

            // Log logging columns headers.
            PrintHeader();
        }

        public async Task PrintEngineEnvironmentDescriptionAsync(EngineWindows engine)
        {
            log.Info($"\n");
            log.Info($"\n{"File system root:",-25} {engine.Path}");
            log.Info($"\n{"Remote storage root:",-25} {remoteStorageRootPath}");
            log.Info($"\n{"AutoLock:",-25} {engine.AutoLock}");
            log.Info($"\n{"Outgoing sync, ms:",-25} {engine.SyncService.SyncIntervalMs}");


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
            log.Info($"\n{"Indexed state:",-25} {await userFileSystemRootFolder.GetIndexedStateAsync()}");
        }

        /// <summary>
        /// Prints console commands.
        /// </summary>
        public void PrintHelp()
        {
            log.Info("\n\n ----------------------------------------------------------------------------------------");
            log.Info("\n Commands:");
            PrintCommandDescription("Spacebar", "Exit without unregistering (simulate reboot)");
            PrintCommandDescription("Esc", "Unregister file system, delete all files/folders, unregister handlers and exit.");
            PrintCommandDescription("Shift-Esc", "Unregister file system, delete all files/folders, unregister handlers, uninstall developer certificate, unregister sparse package and exit (simulate full uninstall).");
            PrintCommandDescription("e", "Start/stop the Engine and all sync services.");
            PrintCommandDescription("s", "Start/stop synchronization service.");
            PrintCommandDescription("m", "Start/stop remote storage monitor.");
            PrintCommandDescription("d", "Enable/disable debug and performance logging.");
            PrintCommandDescription("l", $"Open log file. ({LogFilePath})");
            PrintCommandDescription("b", "Submit support tickets, report bugs, suggest features. (https://userfilesystem.com/support/)");
            log.Info("\n ----------------------------------------------------------------------------------------");
        }

        private void PrintCommandDescription(string key, string description)
        {
            log.Info($"{Environment.NewLine} {key, 12} - {description,-25}");
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
            string att = GetAttString(e.TargetPath ?? e.SourcePath);
            string process = null;
            byte? priorityHint = null;
            string fileId = null;
            string size = null;

            if (e.OperationContext != null)
            {
                process = System.IO.Path.GetFileName(e.OperationContext.ProcessInfo?.ImagePath);
                priorityHint = e.OperationContext.PriorityHint;
                fileId = (e.OperationContext as IWindowsOperationContext).FileId.ToString();
                size = FormatBytes((e.OperationContext as IWindowsOperationContext).FileSize);
            }

            string sourcePath = e.SourcePath?.FitString(sourcePathWidth, 6);
            string targetPath = e.TargetPath?.FitString(sourcePathWidth, 6);

            string message = Format(DateTimeOffset.Now.ToString("hh:mm:ss.fff"), process, priorityHint?.ToString(), fileId, "", e.ComponentName, e.CallerLineNumber.ToString(), e.CallerMemberName, e.CallerFilePath, e.Message,  sourcePath, att, targetPath);

            if (level == log4net.Core.Level.Error)
            {
                Exception ex = ((EngineErrorEventArgs)e).Exception;
                if (ex != null)
                {
                    message += Environment.NewLine;
                }
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

        private static string Format(string date, string process, string priorityHint, string fileId, string remoteStorageId, string componentName, string callerLineNumber, string callerMemberName, string callerFilePath, string message, string sourcePath, string attributes, string targetPath)
        {
            // {fileId,-18} | {remoteStorageId,-remoteStorageIdWidth}
            return $"{Environment.NewLine}|{date, -12}| {process,-25}| {priorityHint,-5}| {componentName,-26}| {callerLineNumber, 4} | {message,-45}| {sourcePath,-sourcePathWidth} | {attributes, 10 } | {targetPath}";
        }

        /// <summary>
        /// Prints logging data headers.
        /// </summary>
        private void PrintHeader()
        {
            log.Info("\n");
            log.Info(Format("Time", "Process Name", "Prty", "FS ID", "RS ID", "Component", "Line", "Caller Member Name", "Caller File Path", "Message", "Source Path", "Attributes", "Target Path"));
            log.Info(Format("----", "------------", "----", "_____", "_____", "---------", "____", "------------------", "----------------", "-------", "-----------", "----------", "-----------"));
        }

        /// <summary>
        /// Gets file or folder attributes in a human-readable form.
        /// </summary>
        /// <param name="path">Path to the file or folder.</param>
        /// <returns>String that represents file or folder attributes or null if the file/folder is not found.</returns>
        public static string GetAttString(string path)
        {
            if (WindowsFileSystemItem.TryGetAttributes(path, out System.IO.FileAttributes? attributes))
            {
                return WindowsFileSystemItem.GetFileAttributesString(attributes.Value);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Gets formatted file size or null for folders or if the file is not found.
        /// </summary>
        /// <param name="path">Path to a file or folder.</param>
        public static string Size(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            long length;
            try
            {
                length = new FileInfo(path).Length;
            }
            catch
            {
                return null;
            }

            return FormatBytes(length);
        }

        /// <summary>
        /// Formats bytes to string.
        /// </summary>
        /// <param name="length">Bytes to format.</param>
        /// <returns>Human readable bytes string.</returns>
        public static string FormatBytes(long length)
        {
            string[] suf = { "b ", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (length == 0)
            {
                return "0" + suf[0];
            }
            long bytes = Math.Abs(length);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(length) * num).ToString() + suf[place];
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
