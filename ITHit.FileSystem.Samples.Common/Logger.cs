using log4net;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using ITHit.FileSystem;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Implements unified logging.
    /// </summary>
    public class Logger : ILogger
    {
        /// <summary>
        /// Name of the component that is writing to the log.
        /// </summary>
        private string componentName;

        /// <summary>
        /// Log4Net Logger.
        /// </summary>
        protected ILog Log;

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
        public void LogError(string message, string sourcePath = null, string targetPath = null, Exception ex = null)
        {
            string att = FsPath.Exists(sourcePath) ? FsPath.GetAttString(sourcePath) : null;
            Log.Error($"\n{DateTimeOffset.Now} [{Thread.CurrentThread.ManagedThreadId,2}] {componentName,-26}{message,-45} {sourcePath,-80} {att} ", ex);
        }

        /// <inheritdoc/>
        public void LogMessage(string message, string sourcePath = null, string targetPath = null)
        {
            string att = FsPath.Exists(sourcePath) ? FsPath.GetAttString(sourcePath) : null;
            string size = FsPath.Size(sourcePath);

            Log.Debug($"\n{DateTimeOffset.Now} [{Thread.CurrentThread.ManagedThreadId,2}] {componentName,-26}{message,-45} {sourcePath,-80} {size,7} {att} {targetPath}");
        }
    }
}
