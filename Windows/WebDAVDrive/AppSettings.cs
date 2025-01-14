using Microsoft.Extensions.Configuration;
using System.Reflection;
using System;
using System.IO;
using System.Collections.Generic;

using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Synchronization;


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
        /// Automatic lock timeout in milliseconds.
        /// </summary>
        public double AutoLockTimeoutMs { get; set; }

        /// <summary>
        /// Manual lock timeout in milliseconds.
        /// </summary>
        public double ManualLockTimeoutMs { get; set; }

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
        /// Mark documents locked by other users as read-only for this user and vice versa.
        /// A read-only MS Office document opens in a view-only mode preventing document collisions.
        /// </summary>
        public bool SetLockReadOnly { get; set; }

        /// <summary>
        /// Incoming synchronization mode.
        /// </summary>
        public IncomingSyncModeSetting IncomingSyncMode { get; set; }

        /// <summary>
        /// Compare command settings.
        /// </summary>
        public Dictionary<string, string> Compare { get; set; } = new();
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
                throw new ArgumentNullException("WebDAVServerURLs");
            }
            for (int i = 0; i < settings.WebDAVServerURLs.Length; i++)
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

    /// <summary>
    /// Incoming synchronization mode settings value.
    /// </summary>
    public enum IncomingSyncModeSetting
    {
        /// <summary>
        /// No pulling or pushing from server will be used.
        /// </summary>
        Off = IncomingSyncMode.Disabled,

        /// <summary>
        /// Synchronization using on Sync ID algorithm.
        /// </summary>
        SyncId = IncomingSyncMode.SyncId,

        /// <summary>
        /// Recive Create, update, delete and move notifications via Web Sockets.
        /// </summary>
        CRUD = 2,

        /// <summary>
        /// Synchronization using remote storage pooling.
        /// </summary>
        TimerPooling = IncomingSyncMode.TimerPooling,

        /// <summary>
        /// Select mode automatically. Tries SyncID, than CRUD, than TimerPooling.
        /// </summary>
        Auto = 256
    }
}
