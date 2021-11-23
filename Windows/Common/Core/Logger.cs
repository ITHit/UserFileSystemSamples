using log4net;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using System.Runtime.InteropServices;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Implements unified logging.
    /// </summary>
    public class Logger : ILogger
    {
        /// <summary>
        /// Name of the component that is writing to the log.
        /// </summary>
        private readonly string componentName;

        /// <summary>
        /// Log4Net Logger.
        /// </summary>
        protected readonly ILog Log;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="componentName">Name of the component that is writing to the log.</param>
        /// <param name="logger">Log4Net Logger.</param>
        public Logger(string componentName, ILog logger)
        {
            this.componentName = componentName;
            this.Log = logger;
        }

        /// <inheritdoc/>
        public void LogError(string message, string sourcePath = null, string targetPath = null, Exception ex = null, IOperationContext operationContext = null)
        {
            string att = FsPath.Exists(sourcePath) ? FsPath.GetAttString(sourcePath) : null;
            string process = null;
            byte? priorityHint = null;
            long? clientFileId = null;

            if (operationContext != null)
            {
                ProcessInfo processInfo = Marshal.PtrToStructure<ProcessInfo>(operationContext.ProcessInfo);
                process = System.IO.Path.GetFileName(processInfo.ImagePath);
                priorityHint = operationContext.PriorityHint;
                clientFileId = (operationContext as IWindowsOperationContext).FileId;
            }

            Log.Error($"{Format(DateTimeOffset.Now.ToString("hh:mm:ss.fff"), process, priorityHint?.ToString(), componentName, message, sourcePath, att, targetPath)}{Environment.NewLine}", ex);
        }

        /// <inheritdoc/>
        public void LogMessage(string message, string sourcePath = null, string targetPath = null, IOperationContext operationContext = null)
        {
            string att = FsPath.Exists(sourcePath) ? FsPath.GetAttString(sourcePath) : null;
            string process = null;
            byte? priorityHint = null;
            long? clientFileId = null;
            string size = null;

            if (operationContext != null)
            {
                ProcessInfo processInfo = Marshal.PtrToStructure<ProcessInfo>(operationContext.ProcessInfo);
                process = System.IO.Path.GetFileName(processInfo.ImagePath);
                priorityHint = operationContext.PriorityHint;
                clientFileId = (operationContext as IWindowsOperationContext).FileId;
                size = FsPath.FormatBytes((operationContext as IWindowsOperationContext).FileSize);
            }

            Log.Debug(Format(DateTimeOffset.Now.ToString("hh:mm:ss.fff"), process, priorityHint?.ToString(), componentName, message, sourcePath, att, targetPath));
        }

        private static string Format(string date, string process, string priorityHint, string componentName, string message, string sourcePath, string attributes, string targetPath)
        {
            return $"{Environment.NewLine}|{date, -12}| {process,-25}| {priorityHint,-5}| {componentName,-26}| {message,-45}| {sourcePath,-80}| {attributes, -22}| {targetPath}";
        }

        public static void PrintHeader(ILog logger)
        {
            logger.Info(Format("Time", "Process Name", "Prty", "Component", "Operation", "Source Path", "Attributes", "Target Path"));
            logger.Info(Format("----", "------------", "----", "---------", "---------", "-----------", "----------", "-----------"));
        }
    }
}
