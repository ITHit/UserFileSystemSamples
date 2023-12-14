using System;
using System.IO;
using System.Text.Json;
using CoreWlan;
using Security;

namespace WebDAVCommon
{
	public class SecureStorage
	{
        public const string ExtensionIdentifier = "com.webdav.vfs.app";
        public const string ExtensionDisplayName = "IT Hit WebDAV Drive";

        private const string AppGroupId = "65S3A9JQ35.group.com.webdav.vfs";
        private const string InternalSettingFile = "data.out";

        /// <summary>
        /// Writes value to storage.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="value">Value.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task SetAsync(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            // Because secure storage requires provisioning profile, in case of the development mode, we store credentials in external file.
            string userDataPath = Path.Combine(GetSharedContainerPath(), InternalSettingFile);
            Dictionary<string, string> userData = File.Exists(userDataPath) ? JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(userDataPath)) : new Dictionary<string,string>();

            if (userData.ContainsKey(key))
            {
                userData[key] = value;
            }
            else
            {
                userData.Add(key, value);
            }

            await File.WriteAllTextAsync(userDataPath, JsonSerializer.Serialize(userData));
        }

        /// <summary>
        /// Returns value by key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns></returns>
        public async Task<string> GetAsync(string key)
        {
            string userDataPath = Path.Combine(GetSharedContainerPath(), InternalSettingFile);
            if (File.Exists(userDataPath))
            {
                Dictionary<string, string> userData = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(userDataPath));

                if (userData.ContainsKey(key))
                {
                    return userData[key];
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }        
        }


        public string GetSharedContainerPath()
        {
            return NSFileManager.DefaultManager.GetContainerUrl(AppGroupId)?.Path;
        }

        /// <summary>
        /// Triggers log-in button in file manager.
        /// </summary>
        public async Task RequireAuthenticationAsync()
        {
            await SetAsync("LoginType", "RequireAuthentication");
        }
    }
}

