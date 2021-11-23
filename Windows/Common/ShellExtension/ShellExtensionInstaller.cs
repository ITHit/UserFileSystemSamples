using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Thumbnails;

namespace ITHit.FileSystem.Samples.Common.Windows.ShellExtension
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
        public static void RegisterComServer(params Guid[] clsids)
        {
            foreach (Guid clsid in clsids)
            {
                string serverKey = string.Format(LocalServer32Path, clsid);
                using RegistryKey regKey = Registry.LocalMachine.CreateSubKey(serverKey);
                regKey.SetValue(null, ExePath);
            }
        }

        /// <summary>
        /// Unregister com library for thumbnails
        /// </summary>
        public static void UnregisterComServer(params Guid[] clsids)
        {
            foreach (Guid clsid in clsids)
            {
                // Unregister local server
                string serverKey = string.Format(LocalServer32Path, clsid);
                Registry.LocalMachine.DeleteSubKey(serverKey, throwOnMissingSubKey: false);
            }
        }

        public static bool HandleRegCommand(string regCommand, params Guid[] clsids)
        {
            if (regCommand.Equals("/regserver", StringComparison.OrdinalIgnoreCase) || regCommand.Equals("-regserver", StringComparison.OrdinalIgnoreCase))
            {
                // Register local server and type library
                RegisterComServer(clsids);
                return true;
            }
            else if (regCommand.Equals("/unregserver", StringComparison.OrdinalIgnoreCase) || regCommand.Equals("-unregserver", StringComparison.OrdinalIgnoreCase))
            {
                // Unregister local server and type library
                UnregisterComServer(clsids);
                return true;
            }

            return false;
        }
    }
}
