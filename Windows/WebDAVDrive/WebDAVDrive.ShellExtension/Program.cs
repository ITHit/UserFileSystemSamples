using ITHit.FileSystem.Windows.ShellExtension;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage.Provider;

namespace WebDAVDrive.ShellExtension
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
