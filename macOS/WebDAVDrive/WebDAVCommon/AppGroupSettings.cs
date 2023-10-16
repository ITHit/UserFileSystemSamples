using System;
using System.IO;
using System.Text.Json;
using Common.Core;
using FileProvider;
using Foundation;

namespace WebDAVCommon
{
    public static class AppGroupSettings
    {
        public static Lazy<AppSettings> Settings = new Lazy<AppSettings>(() =>
        {           
            return BaseAppGroupSettings.ReadAppSettingsFile<AppSettings>(NSBundle.MainBundle.PathForResource("appsettings", "json")); 
        });
    }
}
