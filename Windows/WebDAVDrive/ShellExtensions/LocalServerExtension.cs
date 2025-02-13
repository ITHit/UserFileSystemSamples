using Windows.Storage.Provider;

using ITHit.FileSystem.Windows.ShellExtension;

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

            server.RegisterClass<ThumbnailProviderIntegrated>();
            server.RegisterClass<ContextMenuVerbIntegratedLock>();
            server.RegisterClass<ContextMenuVerbIntegratedCompare>();
            server.RegisterClass<ContextMenuVerbIntegratedUnmount>();
            server.RegisterWinRTClass<IStorageProviderItemPropertySource, CustomStateProviderIntegrated>();
            //server.RegisterWinRTClass<IStorageProviderUriSource, ShellExtension.UriSourceIntegrated>();

            return server;
        }
    }
}
