using System;
using System.IO;
using System.Text.Json;
using Common.Core;
using CoreWlan;
using Security;

namespace WebDAVCommon
{
	public class SecureStorage: SecureStorageBase
    {
        public const string ExtensionIdentifier = "com.webdav.vfs.app";
        public const string ExtensionDisplayName = "IT Hit WebDAV Drive";

        public SecureStorage(): base("65S3A9JQ35.group.com.webdav.vfs")
        {

        }
    }
}

