using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace VirtualDrive.ThumbnailProvider
{
    /// <summary>
    /// Thumbnail COM registartion helper methods. 
    /// </summary>
    /// <remarks>
    /// These methods are NOT required in case of intallation via packaging. 
    /// You can use them f you need to register/unregister COM thumbnail in your msi installer.
    /// </remarks>
    public static class ThumbnailInstaller
    {
        /// <summary>
        /// Register com library for thumbnails
        /// </summary>
        public static void Register()
        {
            string path = typeof(ThumbnailProvider).Assembly.Location.Replace(".dll", ".comhost.dll");
            ExecuteCommand("regsvr32", $"/s {path}");
        }

        /// <summary>
        /// Unregister com library for thumbnails
        /// </summary>
        public static void Unregister()
        {
            string path = typeof(ThumbnailProvider).Assembly.Location.Replace(".dll", ".comhost.dll");
            ExecuteCommand("regsvr32", $"/s /u {path}");
        }

        private static void ExecuteCommand(string executablePath, string args)
        {
            Process proc = new Process();
            proc.StartInfo.FileName = executablePath;
            proc.StartInfo.Arguments = args;
            proc.Start();
        }
    }
}
