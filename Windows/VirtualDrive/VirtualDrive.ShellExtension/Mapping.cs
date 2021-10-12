using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using VirtualDrive.ShellExtension.Settings;

namespace VirtualDrive.ShellExtension
{
    /// <summary>
    /// Maps a user file system path to the remote storage path and back. 
    /// </summary>
    internal static class Mapping
    {
        public static AppSettings AppSettings = ReadAppSettings();  

        /// <summary>
        /// Returns a remote storage URI that corresponds to the user file system path.
        /// </summary>
        public static string MapPath(string userFileSystemPath)
        {
            string relativePath = Path.TrimEndingDirectorySeparator(userFileSystemPath).Substring(
                Path.TrimEndingDirectorySeparator(AppSettings.UserFileSystemRootPath).Length);

            string path = $"{Path.TrimEndingDirectorySeparator(AppSettings.RemoteStorageRootPath)}{relativePath}";
            return path;
        }

        /// <summary>
        /// Reads settings from appsettings.json.
        /// </summary>
        private static AppSettings ReadAppSettings()
        {
            string assemblyPath = Path.GetDirectoryName(typeof(Mapping).Assembly.Location);

            AppSettings settings = AppSettings.Load();

            // Update settings
            if (!Path.IsPathRooted(settings.RemoteStorageRootPath))
            {
                string remoteStorageRootPath = string.Empty;
                if (assemblyPath.Contains("WindowsApps"))
                {
                    // Path to RemoteStorage for msix package.
                    string applicationDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), settings.AppID);
                    remoteStorageRootPath = Path.Combine(applicationDataPath, "RemoteStorage");                  
                }
                else
                {
                    // Path to RemoteStorage folder when run VirtualDrive.Package project directly.
                    remoteStorageRootPath = Path.GetFullPath(Path.Combine(assemblyPath, "..", "..", "..", "..", "..", "..", settings.AppID, settings.RemoteStorageRootPath));
                }
                settings.RemoteStorageRootPath = remoteStorageRootPath;
            }

            if (!Directory.Exists(settings.UserFileSystemRootPath))
            {
                settings.UserFileSystemRootPath = Environment.ExpandEnvironmentVariables(settings.UserFileSystemRootPath);
            }
            return settings;
        }
    }
}
