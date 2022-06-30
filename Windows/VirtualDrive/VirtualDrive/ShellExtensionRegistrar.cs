using System.Reflection;
using log4net;
using CommonShellExtension = ITHit.FileSystem.Windows.ShellExtension;
using VirtualDrive.ShellExtension;
using Windows.Storage.Provider;
using ITHit.FileSystem.Windows.ShellExtension.ComInfrastructure;

namespace VirtualDrive
{
    internal class ShellExtensionRegistrar
    {
        /// <summary>
        /// Registers shell service providers COM classes as well as registers them for sync root. Needed only when process is not running with application or package identity.
        /// </summary>
        /// <param name="syncRootId">Sync root identifier.</param>
        /// <param name="log">Logger.</param>
        internal static void Register(string syncRootId, ILog log)
        {
            log.Info("\nRegistering shell extensions...\n");

            string comServerPath = Assembly.GetEntryAssembly().Location;

            CommonShellExtension.ShellExtensionRegistrar.RegisterHandler(syncRootId, "ThumbnailProvider", typeof(ThumbnailProviderIntegrated).GUID, comServerPath);
            CommonShellExtension.ShellExtensionRegistrar.RegisterHandler(syncRootId, "MenuVerbHandler_0", typeof(ContextMenuVerbIntegrated).GUID, comServerPath);
            CommonShellExtension.ShellExtensionRegistrar.RegisterHandler(syncRootId, "CustomStateHandler", typeof(CustomStateProviderIntegrated).GUID, comServerPath);
            CommonShellExtension.ShellExtensionRegistrar.RegisterHandler(syncRootId, "UriHandler", typeof(UriSourceIntegrated).GUID, comServerPath);
        }

        /// <summary>
        /// Unregisters shell service providers COM classes. Needed only when process is not running with application or package identity.
        /// </summary>
        /// <param name="log">Logger.</param>
        internal static void Unregister(ILog log)
        {
            log.Info("\nUnregistering shell extensions...\n");

            CommonShellExtension.ShellExtensionRegistrar.UnregisterHandler(typeof(ThumbnailProviderIntegrated).GUID);
            CommonShellExtension.ShellExtensionRegistrar.UnregisterHandler(typeof(ContextMenuVerbIntegrated).GUID);
            CommonShellExtension.ShellExtensionRegistrar.UnregisterHandler(typeof(CustomStateProviderIntegrated).GUID);
            CommonShellExtension.ShellExtensionRegistrar.UnregisterHandler(typeof(UriSourceIntegrated).GUID);
        }

        /// <summary>
        /// Registers shell extension class objects with EXE COM server so other applications can connect to them.
        /// </summary>
        internal static void RegisterHandlerClasses(LocalServer server)
        {
            server.RegisterClass<ThumbnailProviderIntegrated>();
            server.RegisterClass<ContextMenuVerbIntegrated>();
            server.RegisterWinRTClass<IStorageProviderItemPropertySource, CustomStateProviderIntegrated>();
            server.RegisterWinRTClass<IStorageProviderUriSource, UriSourceIntegrated>();
        }

        /// <summary>
        /// Returns true if the process started with "-Embedding" command line parameter. Usually this happens when COM is launching the process.
        /// Also see <see href="https://docs.microsoft.com/en-us/windows/win32/com/localserver32#remarks">here</see>.
        /// </summary>
        internal static bool IsEmbedding(string[] args)
        {
            return args.Length == 1 && args[0] == "-Embedding";
        }

        /// <summary>
        /// Returns true if the process started with "-Install" command line parameter. Usually this happens automatically on a post-build event.
        /// </summary>
        internal static bool IsInstall(string[] args)
        {
            return args.Length == 1 && args[0] == "-Install";
        }

        /// <summary>
        /// Returns true if the process started with "-Uninstall" command line parameter.
        /// </summary>
        internal static bool IsUninstall(string[] args)
        {
            return args.Length == 1 && args[0] == "-Uninstall";
        }
    }
}
