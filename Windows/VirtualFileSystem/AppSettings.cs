using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using ITHit.FileSystem.Samples.Common;

namespace VirtualFileSystem
{
    /// <summary>
    /// Strongly binded project settings.
    /// </summary>
    public class AppSettings : Settings
    {
        /// <summary>
        /// Folder that contains file structure to simulate data for your virtual file system.
        /// </summary>
        /// <remarks>
        /// In your real-life application you will read data from your cloud storage, database or any other location, instead of this folder.
        /// </remarks>
        public string RemoteStorageRootPath { get; set; }
    }

    /// <summary>
    /// Binds, validates and normalizes Settings configuration.
    /// </summary>
    public static class SettingsConfigValidator
    {
        /// <summary>
        /// Binds, validates and normalizes WebDAV Context configuration.
        /// </summary>
        /// <param name="configurationSection">Instance of <see cref="IConfigurationSection"/>.</param>
        /// <param name="settings">Virtual File System Settings.</param>
        public static AppSettings ReadSettings(this IConfiguration configuration)
        {
            AppSettings settings = new AppSettings();

            if (configuration == null)
            {
                throw new ArgumentNullException("configurationSection");
            }

            configuration.Bind(settings);

            if (string.IsNullOrEmpty(settings.RemoteStorageRootPath))
            {
                throw new ArgumentNullException("Settings.RemoteStorageRootPath");
            }

            if (string.IsNullOrEmpty(settings.UserFileSystemRootPath))
            {
                throw new ArgumentNullException("Settings.UserFileSystemRootPath");
            }

            if (!Path.IsPathRooted(settings.RemoteStorageRootPath))
            {
                settings.RemoteStorageRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", settings.RemoteStorageRootPath));
            }

            if (!Directory.Exists(settings.RemoteStorageRootPath))
            {
                throw new DirectoryNotFoundException(string.Format("Settings.RemoteStorageRootPath specified in appsettings.json is invalid: '{0}'.", settings.RemoteStorageRootPath));
            }

            if (!Directory.Exists(settings.UserFileSystemRootPath))
            {
                settings.UserFileSystemRootPath = Environment.ExpandEnvironmentVariables(settings.UserFileSystemRootPath);
            }

        
            // Icons folder.
            settings.IconsFolderPath = Path.Combine(AppContext.BaseDirectory, @"Images");

            // Load product name from entry exe file.
            object[] attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
            if (attributes.Length > 0)
            {
                settings.ProductName = (attributes[0] as AssemblyProductAttribute).Product;
            }


            return settings;
        }
    }
}
