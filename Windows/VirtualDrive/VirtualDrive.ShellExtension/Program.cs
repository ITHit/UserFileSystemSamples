using System;
using System.Configuration;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using VirtualDrive.ShellExtension.ComInfrastructure;
using VirtualDrive.ShellExtension.Thumbnails;

namespace VirtualDrive.ShellExtension
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Typically you will register COM exe server in packaged installer, inside Package.appxmanifest.
            // This code is used for testing purposes only, it will register COM exe if you run this program directly.  
            if (args.Length == 1)
            {
                string regCommandMaybe = args[0];
                if (regCommandMaybe.Equals("/regserver", StringComparison.OrdinalIgnoreCase) || regCommandMaybe.Equals("-regserver", StringComparison.OrdinalIgnoreCase))
                {
                    // Register local server and type library
                    ShellExtensionInstaller.RegisterComServer(ThumbnailProvider.ThumbnailClassGuid);
                    ShellExtensionInstaller.RegisterComServer(ContextMenusProvider.ContextMenusClassGuid);
                    return;
                }
                else if (regCommandMaybe.Equals("/unregserver", StringComparison.OrdinalIgnoreCase) || regCommandMaybe.Equals("-unregserver", StringComparison.OrdinalIgnoreCase))
                {
                    // Unregister local server and type library
                    ShellExtensionInstaller.UnregisterComServer(ThumbnailProvider.ThumbnailClassGuid);
                    ShellExtensionInstaller.UnregisterComServer(ContextMenusProvider.ContextMenusClassGuid);
                    return;
                }
            }

            using (var server = new LocalServer())
            {
                server.RegisterClass<ThumbnailProvider>(ThumbnailProvider.ThumbnailClassGuid);
                server.RegisterClass<ContextMenusProvider>(ContextMenusProvider.ContextMenusClassGuid);

                await server.Run();
            }
        }
    }
}
