using System.Reflection;
using log4net;
using CommonShellExtension = ITHit.FileSystem.Windows.ShellExtension;
using WebDAVDrive.ShellExtension;
using Windows.Storage.Provider;
using System.Collections.Generic;
using System;
using ITHit.FileSystem.Windows.ShellExtension;

namespace WebDAVDrive
{
    internal class ShellExtensionRegistrar
    {
        private static List<(string Name, Guid Guid)> handlers = new List<(string, Guid)>
        {
            ("ThumbnailProvider", typeof(ThumbnailProviderIntegrated).GUID),
            ("MenuVerbHandler_0", typeof(ContextMenuVerbIntegrated).GUID),
            ("CustomStateHandler", typeof(CustomStateProviderIntegrated).GUID),
        };

        /// <summary>
        /// Registers shell service providers COM classes as well as registers them for sync root. Needed only when process is not running with application or package identity.
        /// </summary>
        /// <param name="syncRootId">Sync root identifier.</param>
        /// <param name="log">Logger.</param>
        internal static void Register(string syncRootId, ILog log)
        {
            string comServerPath = Assembly.GetEntryAssembly().Location;

            foreach (var handler in handlers)
            {
                log.Info($"\nRegistering shell extension {handler.Name} with CLSID {handler.Guid:B}...\n");

                CommonShellExtension.ShellExtensionRegistrar.RegisterHandler(syncRootId, handler.Name, handler.Guid, comServerPath);
            }
        }

        /// <summary>
        /// Unregisters shell service providers COM classes. Needed only when process is not running with application or package identity.
        /// </summary>
        /// <param name="log">Logger.</param>
        internal static void Unregister(ILog log)
        {
            foreach (var handler in handlers)
            {
                if (CommonShellExtension.ShellExtensionRegistrar.IsHandlerRegistered(handler.Guid))
                {
                    log.Info($"\nUnregistering shell extension {handler.Name} with CLSID {handler.Guid:B}...\n");

                    CommonShellExtension.ShellExtensionRegistrar.UnregisterHandler(handler.Guid);
                }
            }
        }

        /// <summary>
        /// Registers shell extension class objects with EXE COM server so other applications can connect to them.
        /// </summary>
        internal static void RegisterHandlerClasses(LocalServer server)
        {
            server.RegisterClass<ThumbnailProviderIntegrated>();
            server.RegisterClass<ContextMenuVerbIntegrated>();
            server.RegisterWinRTClass<IStorageProviderItemPropertySource, CustomStateProviderIntegrated>();
        }
    }
}
