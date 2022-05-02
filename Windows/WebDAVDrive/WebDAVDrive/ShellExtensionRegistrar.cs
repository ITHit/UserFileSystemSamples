using ITHit.FileSystem.Samples.Common.Windows;
using System.IO;
using System.Reflection;
using WebDAVDrive.ShellExtension;
using CommonShellExtension = ITHit.FileSystem.Samples.Common.Windows.ShellExtension;

namespace WebDAVDrive
{
    internal class ShellExtensionRegistrar
    {
        private static readonly string ComServerRelativePath = @"WebDAVDrive.ShellExtension.exe";

        public static void Register(string syncRootId)
        {
            if (!Registrar.IsRunningAsUwp())
            {
                string comServerPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), ComServerRelativePath));
                CommonShellExtension.ShellExtensionRegistrar.Register(syncRootId, "ThumbnailProvider", typeof(ThumbnailProvider).GUID, comServerPath);
                CommonShellExtension.ShellExtensionRegistrar.Register(syncRootId, "MenuVerbHandler_0", typeof(ContextMenusProvider).GUID, comServerPath);
            }
        }

        public static void Unregister()
        {
            if (!Registrar.IsRunningAsUwp())
            {
                CommonShellExtension.ShellExtensionRegistrar.Unregister(typeof(ThumbnailProvider).GUID);
                CommonShellExtension.ShellExtensionRegistrar.Unregister(typeof(ContextMenusProvider).GUID);
            }
        }

    }
}
