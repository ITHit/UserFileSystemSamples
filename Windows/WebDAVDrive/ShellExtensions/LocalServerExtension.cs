using Windows.Storage.Provider;

using ITHit.FileSystem.Windows.ShellExtension;
using System.Reflection;
using System;

namespace WebDAVDrive.ShellExtensions
{
    /// <summary>
    /// Shell extension handlers registration and startup functionality.
    /// </summary>
    internal static class LocalServerExtension
    {
        /// <summary>
        /// Runs shell extensions COM server and registers shell extension class objects with EXE COM server
        /// so other applications (Windows Explorer) can connect to COMs running in this app.
        /// </summary>
        /// <returns><see cref="LocalServer"/> instance.</returns>
        internal static LocalServer StartComServer()
        {
            LocalServer server = new LocalServerIntegrarted();

            // Dynamically register classes inheriting from CloudFilesContextMenuVerbIntegratedBase and ThumbnailProviderHandlerBase
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsClass && !type.IsAbstract &&
                    (type.IsSubclassOf(typeof(CloudFilesContextMenuVerbIntegratedBase)) ||
                     type.IsSubclassOf(typeof(ThumbnailProviderHandlerBase))))
                {
                    MethodInfo? registerMethod = typeof(LocalServer).GetMethod("RegisterClass");
                    MethodInfo? genericMethod = registerMethod?.MakeGenericMethod(type);
                    genericMethod?.Invoke(server, null);
                }
                // Register WinRT classes that implement IStorageProviderItemPropertySource    
                else if (type.IsClass && !type.IsAbstract &&
                    typeof(IStorageProviderItemPropertySource).IsAssignableFrom(type))
                {
                    MethodInfo? registerMethod = typeof(LocalServer).GetMethod("RegisterWinRTClass");
                    MethodInfo? genericMethod = registerMethod?.MakeGenericMethod(typeof(IStorageProviderItemPropertySource), type);
                    genericMethod?.Invoke(server, null);
                }
            }   
            //server.RegisterWinRTClass<IStorageProviderUriSource, ShellExtension.UriSourceIntegrated>();

            return server;
        }
    }
}
