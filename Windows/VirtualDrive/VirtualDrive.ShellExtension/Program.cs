using ITHit.FileSystem.Windows.ShellExtension;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage.Provider;

namespace VirtualDrive.ShellExtension
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                using (var server = new LocalServerRpc())
                {
                    server.RegisterClass<ThumbnailProviderRpc>();
                    server.RegisterClass<ContextMenuVerbRpc>();
                    server.RegisterClass<StorageProviderCopyHookRpc>();
                    server.RegisterWinRTClass<IStorageProviderItemPropertySource, CustomStateProviderRpc>();
                    server.RegisterWinRTClass<IStorageProviderUriSource, UriSourceRpc>();

                    await server.RunAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
