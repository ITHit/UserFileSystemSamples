using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem.Samples.Common;
using WebDAVDrive.UI;

namespace WebDAVDrive
{
    /// <summary>
    /// Strongly binded application settings.
    /// </summary>
    public class AppSettings : Settings
    {
        /// <summary>
        /// IT Hit WebDAV Client Library for .NET License string;
        /// </summary>
        public string WebDAVClientLicense { get; set; }

        /// <summary>
        /// WebDAV server URL.
        /// </summary>
        public string WebDAVServerUrl { get; set; }
    }

    /// <summary>
    /// Binds, validates and normalizes application settings.
    /// </summary>
    public static class SettingsConfigValidator
    {
        /// <summary>
        /// Binds, validates and normalizes application configuration.
        /// </summary>
        /// <param name="configuration">Application configuration properties.</param>
        /// <returns>Application settings.</returns>
        public static AppSettings ReadSettings(this IConfiguration configuration)
        {
            AppSettings settings = new AppSettings();

            if (configuration == null)
            {
                throw new ArgumentNullException("configurationSection");
            }

            string assemblyLocation = Assembly.GetEntryAssembly().Location;

            // Load product name from entry exe file.
            settings.ProductName = FileVersionInfo.GetVersionInfo(assemblyLocation).ProductName;

            configuration.Bind(settings);

            if (string.IsNullOrEmpty(settings.WebDAVServerUrl))
            {
                settings.WebDAVServerUrl = RegistryManager.GetURL(settings);
            }

            settings.WebDAVServerUrl = $"{settings.WebDAVServerUrl.TrimEnd('/')}/";

            if (string.IsNullOrEmpty(settings.UserFileSystemRootPath))
            {
                throw new ArgumentNullException("Settings.UserFileSystemRootPath");
            }

            settings.UserFileSystemRootPath = $"{Path.TrimEndingDirectorySeparator(settings.UserFileSystemRootPath)}{Path.DirectorySeparatorChar}";

            if (!Directory.Exists(settings.UserFileSystemRootPath))
            {
                settings.UserFileSystemRootPath = Environment.ExpandEnvironmentVariables(settings.UserFileSystemRootPath);
            }

            if (!Directory.Exists(settings.UserFileSystemRootPath))
            {
                Directory.CreateDirectory(settings.UserFileSystemRootPath);
            }
           
            // Icons folder.
            settings.IconsFolderPath = Path.Combine(Path.GetDirectoryName(assemblyLocation), @"Images");

            // Folder where eTags and file locks are stored.
            string localApplicationDataFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            settings.ServerDataFolderPath = Path.Combine(localApplicationDataFolderPath, settings.AppID, settings.UserFileSystemRootPath.Replace(":", ""), "ServerData");
            return settings;
        }       
    }
}
