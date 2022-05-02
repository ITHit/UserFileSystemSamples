using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension.ComInfrastructure;
using ITHit.FileSystem.Samples.Common;
using VirtualDrive.Common;

namespace VirtualDrive.ShellExtension
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Load and initialize settings.
            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build();
            Settings settings = configuration.ReadSettings();

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
