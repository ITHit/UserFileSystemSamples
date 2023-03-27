using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Reflection;

using ITHit.FileSystem.Samples.Common;
using System;
using System.IO;

namespace VirtualDrive
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

        /// <summary>
        /// Full synchronization interval in milliseconds.
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
                // Path to RemoteStorage folder when Any CPU is selected.  
                string remoteStorageRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", settings.RemoteStorageRootPath));

                if (!Directory.Exists(remoteStorageRootPath))
                {
                    // Path to RemoteStorage folder when x64/x86 is selected.
                    remoteStorageRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", settings.RemoteStorageRootPath));
                    
                    if (AppContext.BaseDirectory.Contains("WindowsApps"))
                    {
                        // Path to RemoteStorage for msix package.
                        string applicationDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), settings.AppID);
                        remoteStorageRootPath = Path.Combine(applicationDataPath, "RemoteStorage");

                        if (!Directory.Exists(applicationDataPath))
                        {
                            Directory.CreateDirectory(applicationDataPath);
                        }

                        if (!Directory.Exists(remoteStorageRootPath))
                        {
                            // Copy RemoteStorage folder to ProgramData folder.
                            CopyDirectoryRecursively(new DirectoryInfo(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, settings.RemoteStorageRootPath))),
                                new DirectoryInfo(remoteStorageRootPath));
                        }
                    }
                    else if (!Directory.Exists(remoteStorageRootPath))
                    {
                        // Path to RemoteStorage folder when run VirtualDrive.Package project directly.
                        remoteStorageRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", settings.AppID, settings.RemoteStorageRootPath));
                    }
                }

                settings.RemoteStorageRootPath = remoteStorageRootPath;
            }

            if (!Path.IsPathRooted(settings.ShellExtensionsComServerExePath))
            {
                settings.ShellExtensionsComServerExePath = !string.IsNullOrWhiteSpace(settings.ShellExtensionsComServerExePath) ? Path.Combine(AppContext.BaseDirectory, settings.ShellExtensionsComServerExePath) : null;
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

        /// <summary>
        /// Copies directory.
        /// </summary>
        public static void CopyDirectoryRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
            {
                CopyDirectoryRecursively(dir, target.CreateSubdirectory(dir.Name));
            }

            foreach (FileInfo file in source.GetFiles())
            {
                file.CopyTo(Path.Combine(target.FullName, file.Name));
            }
        }
    }
}
