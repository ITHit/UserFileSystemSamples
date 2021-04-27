using System;
using System.IO;
using FileProvider;
using Foundation;

namespace VirtualFilesystemCommon
{
    public static class AppGroupSettings
    {
        private const string AppGroupId = "${TeamIdentificator}.group.com.userfilesystem.vfs";
        private const string InternalSettingFile = "data.out";
        public const string LocalPathId = "RemoteStorageRootPath";
        public const string LicenseId = "License";

        public static string GetRemoteRootPath()
        {
            NSUrl userDataPath = GetSharedContainerUrl();
            NSDictionary userData = NSDictionary.FromFile(Path.Combine(userDataPath.Path, InternalSettingFile));
            if (userData is null)
            {
                return null;
            }

            return userData.ValueForKey((NSString)LocalPathId).ToString();
        }

        public static string GetUserRootPath()
        {
            return NSFileProviderItemIdentifier.RootContainer;
        }

        public static string GetLicense()
        {
            NSUrl userDataPath = GetSharedContainerUrl();
            NSDictionary userData = NSDictionary.FromFile(Path.Combine(userDataPath.Path, InternalSettingFile));
            if (userData is null)
            {
                return null;
            }

            return userData.ValueForKey((NSString)LicenseId).ToString();
        }

        public static NSDictionary SaveSharedSettings(string resourceName)
        {
            NSDictionary sharedData = ReadContainerSettingsFile(resourceName);
            NSUrl userDataPath = GetSharedContainerUrl();
            if (!sharedData.WriteToFile(Path.Combine(userDataPath.Path, InternalSettingFile), true))
            {
                throw new Exception("Failed to save server setting");
            }

            return sharedData;
        }

        private static NSDictionary ReadContainerSettingsFile(string resourceName)
        {
            string filename = Path.GetFileNameWithoutExtension(resourceName);
            string fileExtension = Path.GetExtension(resourceName)?.Replace(".", "");

            string pathToSettings = NSBundle.MainBundle.PathForResource(filename, fileExtension);
            NSData settingsData = NSData.FromUrl(NSUrl.FromFilename(pathToSettings));

            NSError error;
            NSDictionary jsonObject = (NSDictionary)NSJsonSerialization.Deserialize(settingsData, NSJsonReadingOptions.MutableLeaves, out error);
            if (error is not null)
            {
                throw new Exception(error.ToString());
            }

            return jsonObject;
        }

        private static NSUrl GetSharedContainerUrl()
        {
            return NSFileManager.DefaultManager.GetContainerUrl(AppGroupId);
        }
    }
}
