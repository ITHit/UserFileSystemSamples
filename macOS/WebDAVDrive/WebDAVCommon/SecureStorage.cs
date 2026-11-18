using System;
using System.IO;
using System.Text.Json;
using Common.Core;
using Security;

namespace WebDAVCommon
{
	public class SecureStorage: SecureStorageBase
    {
        // On macOS a sandboxed process can only access app-group containers whose
        // identifier is prefixed with the developer Team ID. iOS uses the
        // "group." prefixed identifier.
#if IOS
        private const string AppGroupId = "group.com.test.webdavdrive";
#else
        private static string AppGroupId => Foundation.NSBundle.MainBundle.ObjectForInfoDictionary("SampleAppGroupId")?.ToString()
            ?? throw new InvalidOperationException("SampleAppGroupId is missing from the app bundle's Info.plist.");
#endif

        public SecureStorage(string domainIdentifier) : base(AppGroupId, domainIdentifier)
        {

        }

        public SecureStorage() : base(AppGroupId)
        {

        }
    }
}

