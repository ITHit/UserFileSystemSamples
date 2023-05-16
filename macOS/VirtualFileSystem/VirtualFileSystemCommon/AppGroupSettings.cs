using System;
using System.IO;
using Common.Core;
using FileProvider;
using Foundation;

namespace VirtualFilesystemCommon
{
    public static class AppGroupSettings
    {
        private const string AppGroupId = "65S3A9JQ36.group.com.filesystem.vfs"; //TODO change this to correct group id
        public const string LocalPathId = "RemoteStorageRootPath";
        public const string LicenseId = "License";

        /// <summary>
        /// Returns virtual file root path.
        /// </summary>
        public static string GetUserRootPath()
        {
            return NSFileProviderItemIdentifier.RootContainer;
        }

        /// <summary>
        /// Returns remote root path.
        /// </summary>

        public static string GetRemoteRootPath()
        {
            return BaseAppGroupSettings.GetSettingValue(LocalPathId, AppGroupId);
        }

        /// <summary>
        /// Returns license.
        /// </summary>
        public static string GetLicense()
        {
            return BaseAppGroupSettings.GetSettingValue(LicenseId, AppGroupId);
        }

        /// <summary>
        /// Saves shares settings.
        /// </summary>
        public static NSDictionary SaveSharedSettings(string resourceName)
        {
            return BaseAppGroupSettings.SaveSharedSettings(resourceName, AppGroupId); 
        }
    }
}
