using System;
using System.Text.Json;

namespace Common.Core
{
	public class SecureStorageBase
	{
        private string appGroupId;
        private const string InternalSettingFile = "data.out";

        public SecureStorageBase(string appGroupId)
        {
            this.appGroupId = appGroupId;
        }

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
            Dictionary<string, string> userData = File.Exists(userDataPath) ? JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(userDataPath)) : new Dictionary<string, string>();

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
        public async Task SetAsync<T>(string key, T val) where T : new()
        {
            await SetAsync(key, JsonSerializer.Serialize<T>(val));
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
                string settingsContent = await File.ReadAllTextAsync(userDataPath);
                Dictionary<string, string> userData = JsonSerializer.Deserialize<Dictionary<string, string>>(settingsContent);

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

        /// <summary>
        /// Returns value by key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns></returns>
        public async Task<T> GetAsync<T>(string key) where T: new()
        {
            string strValue = await GetAsync(key);
            return !string.IsNullOrEmpty(strValue)? JsonSerializer.Deserialize<T>(strValue) : default(T);
        }


        public string GetSharedContainerPath()
        {
            return NSFileManager.DefaultManager.GetContainerUrl(appGroupId)?.Path;
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

