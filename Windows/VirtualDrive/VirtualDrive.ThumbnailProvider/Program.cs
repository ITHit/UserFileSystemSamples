using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using VirtualDrive.ThumbnailProvider.ComInfrastructure;

namespace VirtualDrive.ThumbnailProvider
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 1)
            {
                string regCommandMaybe = args[0];
                if (regCommandMaybe.Equals("/regserver", StringComparison.OrdinalIgnoreCase) || regCommandMaybe.Equals("-regserver", StringComparison.OrdinalIgnoreCase))
                {
                    // Register local server and type library
                    ThumbnailInstaller.Register(ThumbnailProvider.ThumbnailClassGuid);
                    return;
                }
                else if (regCommandMaybe.Equals("/unregserver", StringComparison.OrdinalIgnoreCase) || regCommandMaybe.Equals("-unregserver", StringComparison.OrdinalIgnoreCase))
                {
                    // Unregister local server and type library
                    ThumbnailInstaller.Unregister(ThumbnailProvider.ThumbnailClassGuid);
                    return;
                }
            }

            using (var server = new LocalServer())
            {
                server.RegisterClass<ThumbnailProvider>(ThumbnailProvider.ThumbnailClassGuid);
                
                await server.Run();
            }
        }
    }
}
