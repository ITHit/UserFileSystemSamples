using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension.ComInfrastructure;
using ITHit.FileSystem.Samples.Common;
using System.Diagnostics;

namespace WebDAVDrive.ShellExtension
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Load and initialize settings.
            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build();
            Settings settings = new Settings();
            configuration.Bind(settings);

            ShellExtensionConfiguration.Initialize(settings);

            try
            {
                using (var server = new LocalServer())
                {
                    server.RegisterClass<ThumbnailProvider>(typeof(ThumbnailProvider).GUID);
                    server.RegisterClass<ContextMenusProvider>(typeof(ContextMenusProvider).GUID);

                    await server.Run();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
