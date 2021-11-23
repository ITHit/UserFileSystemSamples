using System.IO;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension;

namespace VirtualDrive.ShellExtension
{
    /// <summary>
    /// Maps a user file system path to the remote storage path and back. 
    /// </summary>
    internal static class Mapping
    {
        public static AppSettings AppSettings => ShellExtensionConfiguration.AppSettings as AppSettings;

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
    }
}
