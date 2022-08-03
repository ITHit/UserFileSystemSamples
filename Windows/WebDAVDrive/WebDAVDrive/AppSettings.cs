using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using ITHit.FileSystem.Samples.Common;
using WebDAVDrive.UI;

namespace WebDAVDrive
{
    /// <summary>
    /// Strongly binded project settings.
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

        /// <summary>
        /// WebSocket server URL.
        /// </summary>
        public string WebSocketServerUrl { get; set; }

        /// <summary>
        /// Full synchronization interval in milliseconds.
        /// </summary>
        public double SyncIntervalMs { get; set; }

        /// <summary>
        /// Throttling max concurrent requests.
        /// </summary>
        public int? MaxConcurrentRequests { get; set; }

        /// <summary>
        /// URL to get a thumbnail for Windows Explorer thumbnails mode.
        /// Your server must return 404 Not Found if the thumbnail can not be generated.
        /// If incorrect size is returned, the image will be resized by the platform automatically.
        /// </summary>
        public string ThumbnailGeneratorUrl { get; set; }

        /// <summary>
        /// File types to request thumbnails for.
        /// To request thumbnails for specific file types, list file types using '|' separator.
        /// To request thumbnails for all file types set the value to "*".
        /// </summary>
        public string RequestThumbnailsFor { get; set; }

        /// <summary>
        /// Absolute or relative path of external COM server executable.
        /// If empty, will host COM classes in the current process.
        /// </summary>
        public string ShellExtensionsComServerExePath { get; set; }

        /// <summary>
        /// Is RPC server enabled
        /// </summary>
        public bool ShellExtensionsComServerRpcEnabled { get; set; }
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

            if (string.IsNullOrEmpty(settings.WebDAVServerUrl))
            {
                settings.WebDAVServerUrl = RegistryManager.GetURL(settings);
            }

            settings.WebDAVServerUrl = $"{settings.WebDAVServerUrl.TrimEnd('/')}/";

            if (string.IsNullOrEmpty(settings.UserFileSystemRootPath))
            {
                throw new ArgumentNullException("Settings.UserFileSystemRootPath");
            }

            if (!Directory.Exists(settings.UserFileSystemRootPath))
            {
                settings.UserFileSystemRootPath = Environment.ExpandEnvironmentVariables(settings.UserFileSystemRootPath);
            }

            string assemblyLocation = Assembly.GetEntryAssembly().Location;
            string applicationDirectory = Path.GetDirectoryName(assemblyLocation);

            if (!Path.IsPathRooted(settings.ShellExtensionsComServerExePath))
            {
                settings.ShellExtensionsComServerExePath = !string.IsNullOrWhiteSpace(settings.ShellExtensionsComServerExePath) ? Path.Combine(applicationDirectory, settings.ShellExtensionsComServerExePath) : null;
            }

            // Icons folder.
            settings.IconsFolderPath = Path.Combine(applicationDirectory, @"Images");

            // Load product name from entry exe file.
            settings.ProductName = FileVersionInfo.GetVersionInfo(assemblyLocation).ProductName;

            if (!settings.MaxConcurrentRequests.HasValue)
            {
                settings.MaxConcurrentRequests = int.MaxValue;
            }

            return settings;
        }
    }
}
