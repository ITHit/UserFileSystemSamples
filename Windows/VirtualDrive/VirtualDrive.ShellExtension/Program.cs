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
                    server.RegisterClass<ThumbnailProviderRpc>();
                    server.RegisterClass<ContextMenuVerbRpc>();
                    server.RegisterWinRTClass<IStorageProviderItemPropertySource, CustomStateProviderRpc>();
                    server.RegisterWinRTClass<IStorageProviderUriSource, UriSourceRpc>();

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
