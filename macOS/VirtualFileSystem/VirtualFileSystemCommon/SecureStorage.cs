using System;
using Common.Core;

namespace VirtualFileSystemCommon
{
    public class SecureStorage : SecureStorageBase
    {
        public const string BaseExtensionIdentifier = "com.userfilesystem.vfs.app";
        public const string BaseExtensionDisplayName = "IT Hit File System";

        private static string AppGroupId => Foundation.NSBundle.MainBundle.ObjectForInfoDictionary("SampleAppGroupId")?.ToString()
            ?? throw new InvalidOperationException("SampleAppGroupId is missing from the app bundle's Info.plist.");

        public SecureStorage() : base(AppGroupId)
        {

        }
    }
}

