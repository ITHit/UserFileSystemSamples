using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace VirtualDrive.ShellExtension.Settings
{
    public class AppSettings
    {
        public string AppID { get; set; }
        public string UserFileSystemRootPath { get; set; }
        public string RemoteStorageRootPath { get; set; }
        public string RpcCommunicationChannelName { get; set; }

        public static AppSettings Load()
        {
            string assemblyPath = Path.GetDirectoryName(typeof(Mapping).Assembly.Location);
            string path = Path.Combine(assemblyPath, "appsettings.json");
            AppSettings settings = new AppSettings();
            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile(path, false, true).Build();
            configuration.Bind(settings);
            
            return settings;
        }
    }
}
