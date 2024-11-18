using System;
using System.Collections.Generic;
using Windows.Storage.Provider;

using ITHit.FileSystem.Windows.Package;
using ITHit.FileSystem.Windows.ShellExtension;


namespace WebDAVDrive.ShellExtension
{
    /// <summary>
    /// Shell extension handlers registration and startup functionality.
    /// </summary>
    internal static class ShellExtensions
    {
        /// <summary>
        /// List of shell extension handlers. Required for app without identity only.
        /// </summary>
        /// <remarks>
        /// For apps with identity this list is ignored. Shell extensions are installed/uninstalled
        /// via the sparse package manifest or package manifest in this case.
        /// 
        /// Note that on Windows 11 your context menu will be shown under the "Show more options"
        /// menu if your app runs without identity.
        /// </remarks>
        internal static readonly List<(string Name, Guid Guid, bool AlwaysRegister)> Handlers = new List<(string, Guid, bool)>
        {
            ("ThumbnailProvider", typeof(ThumbnailProviderIntegrated).GUID, false),
            ("MenuVerbHandler_0", typeof(ContextMenuVerbIntegratedLock).GUID, false),
            ("MenuVerbHandler_1", typeof(ContextMenuVerbIntegratedCompare).GUID, false),
            ("MenuVerbHandler_2", typeof(ContextMenuVerbIntegratedUnmount).GUID, false),
            ("CustomStateHandler", typeof(CustomStateProviderIntegrated).GUID, false),
            //("UriHandler", typeof(ShellExtension.UriSourceIntegrated).GUID, false)
        };

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
