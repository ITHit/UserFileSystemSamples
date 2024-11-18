using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace WebDAVDrive.Utils
{
    public static class ServiceProviderUtil
    {
        public static IServiceProvider ServiceProvider { get; set; }

        public static T GetService<T>() => ServiceProvider.GetService<T>();
    }
}
