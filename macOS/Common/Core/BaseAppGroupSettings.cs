using System;
using System.IO;
using FileProvider;
using Foundation;

namespace Common.Core
{
    public static class BaseAppGroupSettings
    {
        private const string InternalSettingFile = "data.out";

        public static NSDictionary SaveSharedSettings(string resourceName, string appGroupId)
        {
            NSDictionary sharedData = ReadContainerSettingsFile(resourceName);
            NSUrl userDataPath = GetSharedContainerUrl(appGroupId);
            if (!sharedData.WriteToFile(Path.Combine(userDataPath.Path, InternalSettingFile), true))
            {
                throw new Exception("Failed to save server setting");
            }

            return sharedData;
        }

        public static string GetSettingValue(string key, string appGroupId)
        {
            NSUrl userDataPath = GetSharedContainerUrl(appGroupId);
            NSDictionary userData = NSDictionary.FromFile(Path.Combine(userDataPath.Path, InternalSettingFile));
            if (userData is null)
            {
                return null;
            }

            return userData.ValueForKey((NSString)key).ToString();
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

        private static NSUrl GetSharedContainerUrl(string appGroupId)
        {
            return NSFileManager.DefaultManager.GetContainerUrl(appGroupId);
        }
    }
}
