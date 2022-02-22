using System;
using System.IO;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Microsoft.Extensions.Configuration;


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
