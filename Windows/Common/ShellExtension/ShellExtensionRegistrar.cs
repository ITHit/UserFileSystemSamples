using Microsoft.Win32;
using System;

namespace ITHit.FileSystem.Samples.Common.Windows.ShellExtension
{
    public class ShellExtensionRegistrar
    {
        private static readonly string ClsidKeyPathFormat = @"SOFTWARE\Classes\CLSID\{0:B}";
        private static readonly string LocalServer32PathFormat = @$"{ClsidKeyPathFormat}\LocalServer32";
        private static readonly string SyncRootPathFormat = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\SyncRootManager\{0}";

        /// <summary>
        /// Register shell service provider COM class and register it for sync root.
        /// </summary>
        /// <param name="syncRootId">Sync root identifier</param>
        /// <param name="handlerName">Name of shell service provider</param>
        /// <param name="handlerGuid">CLSID of shell service provider</param>
        /// <param name="comServerPath">Absolute path to COM server executable</param>
        public static void Register(string syncRootId, string handlerName, Guid handlerGuid, string comServerPath)
        {
            string handlerPath = handlerGuid.ToString("B").ToUpper();
            string syncRootPath = string.Format(SyncRootPathFormat, syncRootId);

            using RegistryKey syncRootKey = Registry.LocalMachine.OpenSubKey(syncRootPath, true);
            syncRootKey.SetValue(handlerName, handlerPath);

            string localServer32Path = string.Format(LocalServer32PathFormat, handlerPath);
            using RegistryKey localServer32Key = Registry.CurrentUser.CreateSubKey(localServer32Path);
            localServer32Key.SetValue(null, comServerPath);
        }

        /// <summary>
        /// Unregister shell service provider COM class.
        /// </summary>
        /// <param name="handlerClsid"></param>
        public static void Unregister(Guid handlerClsid)
        {
            string thumbnailProviderGuid = handlerClsid.ToString("B").ToUpper();
            string clsidKeyPath = string.Format(ClsidKeyPathFormat, thumbnailProviderGuid);
            Registry.CurrentUser.DeleteSubKeyTree(clsidKeyPath, false);
        }
    }
}
