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
        /// WebDAV server URLs.
        /// </summary>
        public string[] WebDAVServerURLs { get; set; }

        /// <summary>
        /// Automatic lock timout in milliseconds.
        /// </summary>
        public double AutoLockTimoutMs { get; set; }

        /// <summary>
        /// Manual lock timout in milliseconds.
        /// </summary>
        public double ManualLockTimoutMs { get; set; }

        /// <summary>
        /// Full outgoing synchronization and hydration/dehydration interval in milliseconds.
        /// </summary>
        public double SyncIntervalMs { get; set; }

        /// <summary>
        /// Throttling max of create/update/read concurrent requests.
        /// </summary>
        public int? MaxTransferConcurrentRequests { get; set; }

        /// <summary>
        /// Throttling max of listing/move/delete concurrent requests.
        /// </summary>
        public int? MaxOperationsConcurrentRequests { get; set; }

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

        /// <summary>
        // Mark documents locked by other users as read-only for this user and vice versa.
        // A read-only MS Office document opens in a view-only mode preventing document collisions.
        /// </summary>
        public bool SetLockReadOnly { get; set; }

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


            if (settings.WebDAVServerURLs == null || settings.WebDAVServerURLs.Length == 0)
            {
                settings.WebDAVServerURLs = new string[1] { RegistryManager.GetURL(settings) };
            }
            for (int i=0; i < settings.WebDAVServerURLs.Length; i++)
            {
                settings.WebDAVServerURLs[i] = $"{settings.WebDAVServerURLs[i].TrimEnd('/')}/";
            }

            if (string.IsNullOrEmpty(settings.UserFileSystemRootPath))
            {
                throw new ArgumentNullException("Settings.UserFileSystemRootPath");
            }

            if (!Directory.Exists(settings.UserFileSystemRootPath))
            {
                settings.UserFileSystemRootPath = Environment.ExpandEnvironmentVariables(settings.UserFileSystemRootPath);
            }

            if (!Path.IsPathRooted(settings.ShellExtensionsComServerExePath))
            {
                settings.ShellExtensionsComServerExePath = !string.IsNullOrWhiteSpace(settings.ShellExtensionsComServerExePath) ? Path.Combine(AppContext.BaseDirectory, settings.ShellExtensionsComServerExePath) : null;
            }

            // Icons folder.
            settings.IconsFolderPath = Path.Combine(AppContext.BaseDirectory, @"Images");

            // Load product name from entry exe file.
            object[] attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
            if (attributes.Length > 0)
            {
                settings.ProductName = (attributes[0] as AssemblyProductAttribute).Product;
            }

            if (!settings.MaxTransferConcurrentRequests.HasValue)
            {
                settings.MaxTransferConcurrentRequests = 6;
            }

            if (!settings.MaxOperationsConcurrentRequests.HasValue)
            {
                settings.MaxOperationsConcurrentRequests = int.MaxValue;
            }

            return settings;
        }
    }
}
