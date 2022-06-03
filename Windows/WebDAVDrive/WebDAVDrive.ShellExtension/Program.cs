using System;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.Storage.Provider;

using ITHit.FileSystem.Windows.ShellExtension.ComInfrastructure;


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
                    server.RegisterClass<ThumbnailProvider>();
                    server.RegisterClass<ContextMenusProvider>();
                    server.RegisterWinRTClass<IStorageProviderItemPropertySource, CustomStateProvider>();

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
