using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
        /// Path to the folder that stores custom data associated with files and folders.
        /// </summary>
        public string ServerDataFolderPath { get; set; }

        /// <summary>
        /// Communication channel name is used by RPC to establish connection over named pipes.
        /// </summary>
        public string RpcCommunicationChannelName { get; set; }

        /// <summary>
        /// Full synchronization interval in milliseconds.
        /// </summary>
        public double SyncIntervalMs { get; set; }
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

            // Icons folder.
            settings.IconsFolderPath = Path.Combine(Path.GetDirectoryName(assemblyLocation), @"Images");

            // Load product name from entry exe file.
            settings.ProductName = FileVersionInfo.GetVersionInfo(assemblyLocation).ProductName;

            // Folder where custom data is stored.
            string localApplicationDataFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            settings.ServerDataFolderPath = Path.Combine(localApplicationDataFolderPath, settings.AppID, settings.UserFileSystemRootPath.Replace(":", ""), "ServerData");

            return settings;
        }
    }
}
