using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension.ComInfrastructure;

namespace VirtualDrive.ShellExtension
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Typically you will register COM exe server in packaged installer, inside Package.appxmanifest.
            // This code is used for testing purposes only, it will register COM exe if you run this program directly.  
            if (args.Length == 1 &&
                ShellExtensionInstaller.HandleRegCommand(args[0], ThumbnailProvider.ThumbnailClassGuid, ContextMenusProvider.ContextMenusClassGuid))
            {
                return;
            }

            // Load and initialize settings.
            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build();
            ShellExtensionConfiguration.Initialize(configuration.ReadSettings());

            try
            {
                using (var server = new LocalServer())
                {
                    server.RegisterClass<ThumbnailProvider>(ThumbnailProvider.ThumbnailClassGuid);
                    server.RegisterClass<ContextMenusProvider>(ContextMenusProvider.ContextMenusClassGuid);

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
