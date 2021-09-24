using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;
using VirtualDrive.ShellExtension.Thumbnails;

namespace VirtualDrive.ShellExtension
{
    /// <summary>
    /// Thumbnail COM registartion helper methods. 
    /// </summary>
    /// <remarks>
    /// These methods are NOT required in case of intallation via packaging. 
    /// You can use them if you need to register/unregister COM thumbnail in your msi installer.
    /// </remarks>
    public static class ShellExtensionInstaller
    {
        private static readonly string LocalServer32Path = @"SOFTWARE\Classes\CLSID\{0:B}\LocalServer32";

        private static string ExePath => typeof(ShellExtensionInstaller).Assembly.Location;

        /// <summary>
        /// Register com library for thumbnails
        /// </summary>
        public static void RegisterComServer(Guid clsid)
        {
            string serverKey = string.Format(LocalServer32Path, clsid);
            using RegistryKey regKey = Registry.LocalMachine.CreateSubKey(serverKey);
            regKey.SetValue(null, ExePath);
        }

        /// <summary>
        /// Unregister com library for thumbnails
        /// </summary>
        public static void UnregisterComServer(Guid clsid)
        {
            // Unregister local server
            string serverKey = string.Format(LocalServer32Path, clsid);
            Registry.LocalMachine.DeleteSubKey(serverKey, throwOnMissingSubKey: false);
        }
    }
}
