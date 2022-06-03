using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage.Provider;

using ITHit.FileSystem.Windows.ShellExtension.ComInfrastructure;


namespace VirtualDrive.ShellExtension
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                using (var server = new LocalServer())
                {
                    server.RegisterClass<ThumbnailProvider>();
                    server.RegisterClass<ContextMenusProvider>();
                    server.RegisterWinRTClass<IStorageProviderItemPropertySource, CustomStateProvider>();
                    server.RegisterWinRTClass<IStorageProviderUriSource, UriSource>();

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
