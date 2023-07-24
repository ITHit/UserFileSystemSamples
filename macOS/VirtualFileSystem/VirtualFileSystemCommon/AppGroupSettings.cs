using System;
using System.IO;
using Common.Core;
using FileProvider;
using Foundation;

namespace VirtualFilesystemCommon
{
    public static class AppGroupSettings
    {
        public const string RemoteStorageRootPathId = "RemoteStorageRootPath";
        public const string UserFileSystemLicenseId = "UserFileSystemLicense";


        /// <summary>
        /// Returns remote root path.
        /// </summary>

        public static string GetRemoteRootPath()
        {
            return BaseAppGroupSettings.GetSettingValue(RemoteStorageRootPathId);
        }

        /// <summary>
        /// Returns license.
        /// </summary>
        public static string GetLicense()
        {
            return BaseAppGroupSettings.GetSettingValue(UserFileSystemLicenseId);
        }
    }
}
