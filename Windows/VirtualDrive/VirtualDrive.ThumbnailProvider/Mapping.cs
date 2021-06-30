using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using VirtualDrive.ThumbnailProvider.Settings;

namespace VirtualDrive.ThumbnailProvider
{
    /// <summary>
    /// Maps a user file system path to the remote storage path and back. 
    /// </summary>
    internal static class Mapping
    {
        private static AppSettings appSettings = ReadAppSettings();

        /// <summary>
        /// Returns a remote storage URI that corresponds to the user file system path.
        /// </summary>
        public static string MapPath(string userFileSystemPath)
        {
            string relativePath = Path.TrimEndingDirectorySeparator(userFileSystemPath).Substring(
                Path.TrimEndingDirectorySeparator(appSettings.UserFileSystemRootPath).Length);

            string path = $"{Path.TrimEndingDirectorySeparator(appSettings.RemoteStorageRootPath)}{relativePath}";
            return path;
        }

        /// <summary>
        /// Reads settings from appsettings.json.
        /// </summary>
        private static AppSettings ReadAppSettings()
        {
            string assemblyPath = Path.GetDirectoryName(typeof(Mapping).Assembly.Location);
            string path = Path.Combine(assemblyPath, "appsettings.json");
            AppSettings settings = new AppSettings();
            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile(path, false, true).Build();
            configuration.Bind(settings);

            // Update settings
            if (!Path.IsPathRooted(settings.RemoteStorageRootPath))
            {
                settings.RemoteStorageRootPath = Path.GetFullPath(Path.Combine(assemblyPath, "..", "VirtualDrive", settings.RemoteStorageRootPath));
            }

            if (!Directory.Exists(settings.UserFileSystemRootPath))
            {
                settings.UserFileSystemRootPath = Environment.ExpandEnvironmentVariables(settings.UserFileSystemRootPath);
            }
            return settings;
        }
    }
}
