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
       public static T ReadAppSettingsFile<T>(string path) where T : new()
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip
            };         

            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), options);
        }
    }
}
