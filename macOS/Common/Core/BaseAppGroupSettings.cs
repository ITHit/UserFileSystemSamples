using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FileProvider;
using Foundation;

namespace Common.Core
{
    public static class BaseAppGroupSettings
    {
        private static ConcurrentDictionary<string, string> appSettings = new ConcurrentDictionary<string, string>();


        public static string GetSettingValue(string key)
        {
            if (!appSettings.ContainsKey(key))
            {
                string pathToSettings = NSBundle.MainBundle.PathForResource("appsettings", "json");

                // read all settings.
                foreach (KeyValuePair<string, string> setting in ReadAppSettingsFile(pathToSettings))
                {
                    appSettings.TryAdd(setting.Key, setting.Value);
                }
            }
            return appSettings[key];
        }

        private static Dictionary<string, string> ReadAppSettingsFile(string path)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip
            };         

            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path), options);
        }
    }
}
