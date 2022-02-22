using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension.ComInfrastructure;
using ITHit.FileSystem.Samples.Common;
using System.Reflection;
using System.IO;
using System.Linq;
using log4net;
using log4net.Appender;
using log4net.Config;


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
            Settings settings = configuration.ReadSettings();

            ShellExtensionConfiguration.Initialize(settings);

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
