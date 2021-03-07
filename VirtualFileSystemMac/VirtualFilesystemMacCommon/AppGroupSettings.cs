using System;
using System.IO;
using Foundation;

namespace VirtualFilesystemMacCommon
{
    public static class AppGroupSettings
    {
        private const string AppGroupId = "DHSP75SFA6.group.com.ithit.virtualfilesystem";
        private const string SettingFile = "data.out";
        private const string LocalPathId = "LocalPath";

        public static string GetRootPath()
        {
            NSUrl userDataPath = GetSharedContainerUrl();
            NSDictionary userData = NSDictionary.FromFile(Path.Combine(userDataPath.Path, SettingFile));
            if (userData is null)
            {
                return null;
            }

            return userData.ValueForKey((NSString)LocalPathId).ToString();
        }

        public static void SaveRootPath(string rootPath)
        {
            NSUrl userDataPath = GetSharedContainerUrl();
            NSDictionary userData = new NSDictionary(new NSString(LocalPathId), new NSString(rootPath));

            if (!userData.WriteToFile(Path.Combine(userDataPath.Path, SettingFile), true))
            {
                throw new Exception("Failed to save server setting");
            }
        }

        private static NSUrl GetSharedContainerUrl()
        {
            return NSFileManager.DefaultManager.GetContainerUrl(AppGroupId);
        }
    }
}