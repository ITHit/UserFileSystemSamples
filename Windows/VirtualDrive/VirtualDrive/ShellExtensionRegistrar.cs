using System.IO;
using System.Reflection;
using log4net;
using CommonShellExtension = ITHit.FileSystem.Windows.ShellExtension;
using ITHit.FileSystem.Windows.ShellExtension;
using VirtualDrive.ShellExtension;


namespace VirtualDrive
{
    internal class ShellExtensionRegistrar
    {
        private static readonly string ComServerRelativePath = "VirtualDrive.ShellExtension.exe";

        internal static void Register(string syncRootId, ILog log)
        {
            if (!PackageRegistrar.IsRunningAsUwp())
            {
                log.Info("\nRegistering shell extensions...\n");

                string applicationDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                string comServerPath = Path.Combine(applicationDirectory, ComServerRelativePath);

                CommonShellExtension.ShellExtensionRegistrar.Register(syncRootId, "ThumbnailProvider", typeof(ThumbnailProvider).GUID, comServerPath);
                CommonShellExtension.ShellExtensionRegistrar.Register(syncRootId, "MenuVerbHandler_0", typeof(ContextMenusProvider).GUID, comServerPath);
                CommonShellExtension.ShellExtensionRegistrar.Register(syncRootId, "CustomStateHandler", typeof(CustomStateProvider).GUID, comServerPath);
                CommonShellExtension.ShellExtensionRegistrar.Register(syncRootId, "UriHandler", typeof(UriSource).GUID, comServerPath);
                CommonShellExtension.ShellExtensionRegistrar.RegisterPackage(syncRootId);
            }
        }

        internal static void Unregister(ILog log)
        {
            if (!PackageRegistrar.IsRunningAsUwp())
            {
                log.Info("\nUnregistering shell extensions...\n");

                CommonShellExtension.ShellExtensionRegistrar.Unregister(typeof(ThumbnailProvider).GUID);
                CommonShellExtension.ShellExtensionRegistrar.Unregister(typeof(ContextMenusProvider).GUID);
                CommonShellExtension.ShellExtensionRegistrar.Unregister(typeof(CustomStateProvider).GUID);
                CommonShellExtension.ShellExtensionRegistrar.Unregister(typeof(UriSource).GUID);
            }
        }
    }
}
