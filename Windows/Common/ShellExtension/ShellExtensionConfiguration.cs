using System;
using System.IO;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Microsoft.Extensions.Configuration;

using ITHit.FileSystem.Samples.Common;

namespace ITHit.FileSystem.Samples.Common.Windows.ShellExtension
{
    /// <summary>
    /// ShellExtension configuration class.
    /// </summary>
    public static class ShellExtensionConfiguration
    {
        /// <summary>
        /// ShellExtension settings.
        /// </summary>
        public static Settings AppSettings { get; private set; }

        /// <summary>
        /// Initialize or load settings.
        /// </summary>
        public static void Initialize(Settings settings = null)
        {
            if (settings != null)
            {
                AppSettings = settings;
            }
            else
            {
                AppSettings = Load();
            }
        }

        /// <summary>
        /// Returns is path to virtual drive folder
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IsVirtualDriveFolder(string path)
        {
            string rootPath = Environment.ExpandEnvironmentVariables(AppSettings.UserFileSystemRootPath);

            return !string.IsNullOrEmpty(path) && path.TrimStart().StartsWith(rootPath);
        }

        /// <summary>
        /// Returns logger with configured repository.
        /// </summary>
        public static ILog GetLogger(string logName)
        {
            string assemblyPath = Path.GetDirectoryName(typeof(ShellExtensionConfiguration).Assembly.Location);
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository(typeof(ShellExtensionConfiguration).Assembly);

            if (!hierarchy.Configured)
            {
                PatternLayout patternLayout = new PatternLayout();
                patternLayout.ConversionPattern = "%date [%thread] %-5level %logger - %message%newline";
                patternLayout.ActivateOptions();

                RollingFileAppender roller = new RollingFileAppender();
                roller.AppendToFile = true;
                if (assemblyPath.Contains("WindowsApps"))
                {
                    roller.File = 
                        Path.Combine(
                            Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                                AppSettings.AppID),
                            "ShelIExtension.log");
                }
                else
                {
                    roller.File = Path.Combine(assemblyPath, "ShelIExtension.log");
                }
                roller.Layout = patternLayout;
                roller.MaxSizeRollBackups = 5;
                roller.MaximumFileSize = "10MB";
                roller.RollingStyle = RollingFileAppender.RollingMode.Size;
                roller.StaticLogFileName = true;
                roller.ActivateOptions();
                hierarchy.Root.AddAppender(roller);

                hierarchy.Root.Level = Level.Info;
                hierarchy.Configured = true;
            }

            return LogManager.GetLogger(typeof(ShellExtensionConfiguration));
        }

        private static Settings Load()
        {
            string assemblyPath = Path.GetDirectoryName(typeof(ShellExtensionConfiguration).Assembly.Location);
            string path = Path.Combine(assemblyPath, "appsettings.json");
            Settings settings = new Settings();
            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile(path, false, true).Build();
            configuration.Bind(settings);

            return settings;
        }
    }
}
