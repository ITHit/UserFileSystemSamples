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
using Windows.Storage.Search;
using System.Text;

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
                Log.Info($"{Environment.NewLine}Debug logging {debugLoggingState}");
            }
        }

        public readonly ILog Log;

        private bool debugLoggingEnabled = false;

        private readonly string appId;

        private const int sourcePathWidth = 30;
        private const int remoteStorageIdWidth = 12;

        private const int indent = -45;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="log">Log4net logger.</param>
        public LogFormatter(ILog log, string appId)
        {
            this.Log = log;
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
            XmlConfigurator.Configure(logRepository, new FileInfo(Path.Combine(AppContext.BaseDirectory, "log4net.config")));

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
            Log.Info($"\n{"AppID:",indent} {appId}");
            Log.Info($"\n{"Engine version:",indent} {typeof(IEngine).Assembly.GetName().Version}");
            Log.Info($"\n{"OS version:",indent} {RuntimeInformation.OSDescription}");
            Log.Info($"\n{".NET version:",indent} {RuntimeInformation.FrameworkDescription} {IntPtr.Size * 8}bit.");
            Log.Info($"\n{"Package or app identity:",indent} {PackageRegistrar.IsRunningWithIdentity()}");
            Log.Info($"\n{"Sparse package identity:",indent} {PackageRegistrar.IsRunningWithSparsePackageIdentity()}");
            Log.Info($"\n{"Elevated mode:",indent} {new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator)}");

            string sparsePackagePath = PackageRegistrar.GetSparsePackagePath();
            if (File.Exists(sparsePackagePath))
            {
                Log.Info($"\n{"Sparse package location:",indent} {sparsePackagePath}");
                var cert = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(sparsePackagePath);
                Log.Info($"\n{"Sparse package cert:",indent} Subject: {cert.Subject}, Issued by: {cert.Issuer}");
            }
            else
            {
                Log.Info($"\n{"Sparse package:",indent} Not found");
            }
        }

        /// <summary>
        /// Print Engine config, settings, logging headers. 
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        /// <param name="remoteStorageRootPath">Remote storage root path.</param>
        public async Task PrintEngineStartInfoAsync(EngineWindows engine, string remoteStorageRootPath)
        {
            await PrintEngineDescriptionAsync(engine, remoteStorageRootPath);
            Log.Info("\n");

            // Log logging columns headers.
            PrintHeader();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        /// <param name="remoteStorageRootPath">Remote storage root path.</param>
        /// <returns></returns>
        public async Task PrintEngineDescriptionAsync(EngineWindows engine, string remoteStorageRootPath)
        {
            Log.Info($"\n");
            Log.Info($"\n{"File system root:",indent} {engine.Path}");
            Log.Info($"\n{"Remote storage root:",indent} {remoteStorageRootPath}");
            Log.Info($"\n{"AutoLock:",indent} {engine.AutoLock}");
            Log.Info($"\n{"Outgoing sync, ms:",indent} {engine.SyncService.SyncIntervalMs}");
            Log.Info($"\n{"Sync mode:",indent} {engine.SyncService.IncomingSyncMode}");
            Log.Info($"\n{"Shell extensions RPC enabled:",indent} {engine.ShellExtensionsComServerRpcEnabled}");
            Log.Info($"\n{"Max create/read/write concurrent requests:",indent} {engine.MaxTransferConcurrentRequests}");
            Log.Info($"\n{"Max list/move/delete concurrent requests:",indent} {engine.MaxOperationsConcurrentRequests}");

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
            IndexedState indexedState = await userFileSystemRootFolder.GetIndexedStateAsync();
            Log.Info($"\n{"Indexed state:",indent} {indexedState}");

            if (indexedState != IndexedState.FullyIndexed)
            {
                Log.ErrorFormat($"\nIndexing is disabled. Indexing must be enabled for {path}");
            }
        }

        public void LogMessage(string message)
        {
            Log.Info(message);
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
        /// <param name="e">Engine message, error or debug info.</param>
        /// <param name="level">Log level.</param>
        private void WriteLog(IEngine sender, EngineMessageEventArgs e, log4net.Core.Level level)
        {
            string attSource = GetAttString(e.SourcePath);
            string attTarget = GetAttString(e.TargetPath);

            string remoteStorageId = null;
            string process = null;
            byte? priorityHint = null;
            string fileId = null;
            string size = null;

            if (e.OperationContext != null)
            {
                byte[] rsId = e?.Metadata?.RemoteStorageItemId ?? e.OperationContext.RemoteStorageItemId;
                remoteStorageId = IdToSting(rsId)?.FitString(remoteStorageIdWidth, 4);
                process = System.IO.Path.GetFileName(e.OperationContext?.ProcessInfo?.ImagePath);
                priorityHint = e.OperationContext?.PriorityHint;
                IWindowsOperationContext ocWin = e.OperationContext as IWindowsOperationContext;
                if (ocWin != null)
                {
                    fileId = ocWin.FileId.ToString();
                    size = FormatBytes((e.OperationContext as IWindowsOperationContext).FileSize);
                }
            }

            string sourcePath = e.SourcePath?.FitString(sourcePathWidth, 6);
            string targetPath = e.TargetPath?.FitString(sourcePathWidth, 6);

            string message = Format(DateTimeOffset.Now.ToString("hh:mm:ss.fff"), process, priorityHint?.ToString(), fileId, e.ComponentName, e.CallerLineNumber.ToString(), e.CallerMemberName, e.CallerFilePath, e.Message, remoteStorageId, sourcePath, attSource);
            if (targetPath!=null)
            {
                // For move operation output target path in the next line.
                message += Format(null, null, null, null, null, null, null, null, null, null, targetPath, attTarget);
            }

            if (level == log4net.Core.Level.Error)
            {
                Exception ex = ((EngineErrorEventArgs)e).Exception;
                if (ex != null)
                {
                    message += Environment.NewLine;
                }
                Log.Error(message, ex);
            }
            else if (level == log4net.Core.Level.Info)
            {
                Log.Info(message);
            }
            else if (level == log4net.Core.Level.Debug && DebugLoggingEnabled)
            {
                Log.Debug(message);
            }

        }

        private static string Format(string date, string process, string priorityHint, string fileId, string componentName, string callerLineNumber, string callerMemberName, string callerFilePath, string message, string remoteStorageId, string path, string attributes)
        {
            // {fileId,-18} | {remoteStorageId,-remoteStorageIdWidth}
            return $"{Environment.NewLine}|{date,-12}| {process,-25}| {componentName,-26}| {message,-45}| {remoteStorageId,-remoteStorageIdWidth}| {path,-sourcePathWidth} | {attributes,10}";
        }

        /// <summary>
        /// Prints logging data headers.
        /// </summary>
        private void PrintHeader()
        {
            Log.Info("\n");
            Log.Info(Format("Time", "Process Name", "Prty", "FS ID", "Component", "Line", "Caller Member Name", "Caller File Path", "Message", "RS Item ID", "Path", "Attributes"));
            Log.Info(Format("----", "------------", "----", "_____", "---------", "____", "------------------", "----------------", "-------", "__________", "----", "----------"));
        }

        /// <summary>
        /// Gets file or folder attributes in a human-readable form.
        /// </summary>
        /// <param name="path">Path to the file or folder.</param>
        /// <returns>String that represents file or folder attributes or null if the file/folder is not found.</returns>
        public static string GetAttString(string path)
        {
            if (path == null)
                return null;

            if (WindowsFileSystemItem.TryGetAttributes(path, out System.IO.FileAttributes? attributes))
            {
                return WindowsFileSystemItem.GetFileAttributesString(attributes.Value);
            }
            
            return null;
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

        public static string IdToSting(byte[] remoteStorageItemId)
        {
            if (remoteStorageItemId == null)
                return null;

            switch(remoteStorageItemId.Length)
            {
                case 8:
                    return BitConverter.ToInt64(remoteStorageItemId, 0).ToString();
                case 16:
                    return new Guid(remoteStorageItemId).ToString();
                default:
                    // Try parse URI
                    string uriStrId = Encoding.UTF8.GetString(remoteStorageItemId);
                    if (Uri.TryCreate(uriStrId, UriKind.RelativeOrAbsolute, out Uri uriId) && uriId.IsAbsoluteUri)
                    {
                        return uriId.Segments.Last();
                    }
                    else
                    {
                        return BitConverter.ToString(remoteStorageItemId);
                    }
            }
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
