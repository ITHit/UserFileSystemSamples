using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using VirtualDrive.ThumbnailProvider.Interop;
using VirtualFileSystem.ThumbnailProvider.Settings;

namespace VirtualDrive.ThumbnailProvider
{
    // It is Windows Shell Extension code. We can ignore warnings about platform compatibility
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]

    [ComVisible(true)]
    [ProgId("VirtualFileSystem.ThumbnailProvider"), Guid("c455638f-003a-472d-8391-8bda0ed3cdac")]
    public class ThumbnailProvider : InitializedWithItem, IThumbnailProvider
    {
        private string filePath = null;
        private readonly ILog log;
        private readonly AppSettings appSettings;

        public ThumbnailProvider()
        {
            appSettings = ReadAppSettings();
            log = GetLogger();
        }

        public override int Initialize(IShellItem shellItem, STGM accessMode)
        {
            if (shellItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string path) != WinError.S_OK)
            {
                return WinError.E_UNEXPECTED;
            }

            // Show thumbnails only for files (Dont show thumbnails for directories)
            if (!File.Exists(path))
            {
                return WinError.E_UNEXPECTED;
            }
            filePath = path;

            return base.Initialize(shellItem, accessMode);
        }

        public int GetThumbnail(uint cx, out IntPtr phbmp, out WTS_ALPHATYPE pdwAlpha)
        {
            phbmp = IntPtr.Zero;
            pdwAlpha = WTS_ALPHATYPE.WTSAT_UNKNOWN;

            try
            {
                log.Info($"\nGetting thumbnail for {filePath}");

                string sourcePath = MapPath(filePath);
                int THUMB_SIZE = 256;
                using (Bitmap thumbnail = WindowsThumbnailProvider.GetThumbnail(
                   sourcePath, THUMB_SIZE, THUMB_SIZE, ThumbnailOptions.None))
                {
                    phbmp = thumbnail.GetHbitmap();
                    pdwAlpha = WTS_ALPHATYPE.WTSAT_ARGB;
                }
                return WinError.S_OK;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return WinError.E_FAIL;
            }

        }

        private string MapPath(string userFileSystemPath)
        {
            string relativePath = Path.TrimEndingDirectorySeparator(userFileSystemPath).Substring(
                Path.TrimEndingDirectorySeparator(appSettings.UserFileSystemRootPath).Length);

            string path = $"{Path.TrimEndingDirectorySeparator(appSettings.RemoteStorageRootPath)}{relativePath}";
            return path;
        }

        /// <summary>
        /// Reads settings from appsettings.json.
        /// </summary>
        private static AppSettings ReadAppSettings()
        {
            string assemblyPath = Path.GetDirectoryName(typeof(ThumbnailProvider).Assembly.Location);
            string path = Path.Combine(assemblyPath, "appsettings.json");
            AppSettings settings = new AppSettings();
            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile(path, false, true).Build();
            configuration.Bind(settings);

            // Update settings
            if (!Path.IsPathRooted(settings.RemoteStorageRootPath))
            {
                settings.RemoteStorageRootPath = Path.GetFullPath(Path.Combine(assemblyPath, "..", "..", "..", settings.RemoteStorageRootPath));
            }

            if (!Directory.Exists(settings.UserFileSystemRootPath))
            {
                settings.UserFileSystemRootPath = Environment.ExpandEnvironmentVariables(settings.UserFileSystemRootPath);
            }
            return settings;
        }


        /// <summary>
        /// Returns logger with configured repository.
        /// </summary>
        public static ILog GetLogger()
        {
            string assemblyPath = Path.GetDirectoryName(typeof(ThumbnailProvider).Assembly.Location);
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository(typeof(ThumbnailProvider).Assembly);

            if (!hierarchy.Configured)
            {
                PatternLayout patternLayout = new PatternLayout();
                patternLayout.ConversionPattern = "%date [%thread] %-5level %logger - %message%newline";
                patternLayout.ActivateOptions();

                RollingFileAppender roller = new RollingFileAppender();
                roller.AppendToFile = true;
                roller.File = Path.Combine(assemblyPath, "ThumbnailProvider.log");
                roller.Layout = patternLayout;
                roller.MaxSizeRollBackups = 5;
                roller.MaximumFileSize = "10MB";
                roller.RollingStyle = RollingFileAppender.RollingMode.Size;
                roller.StaticLogFileName = true;
                roller.ActivateOptions();
                hierarchy.Root.AddAppender(roller);

                hierarchy.Root.Level = Level.Info;
                hierarchy.Configured = true;
            }

            return LogManager.GetLogger(typeof(ThumbnailProvider));
        }

        /// <summary>
        /// Registers thumbnail provider to registry.
        /// </summary>
        [ComRegisterFunction]
        internal static void Register(Type t)
        {
            if (t != typeof(ThumbnailProvider))
            {
                return;
            }
            var log = GetLogger();
            log.Info("Register thumbnail provider");
            try
            {
                SetDisableProcessIsolationValue(1);
                ApproveProvider();
                AppSettings settings = ReadAppSettings();
                AddThumbnailProviderToRegistry(settings.AppID);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        /// <summary>
        /// Unregisters thumbnail provider from registry.
        /// </summary>
        [ComUnregisterFunction]
        internal static void Unregister(Type t)
        {
            if (t != typeof(ThumbnailProvider))
            {
                return;
            }
            var log = GetLogger();
            log.Info("Unregister thumbnail provider");
            try
            {
                AppSettings settings = ReadAppSettings();
                RemoveThumbnailProviderFromRegistry(settings.AppID);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private static void ApproveProvider()
        {
            using (RegistryKey approvedKey = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved", 
                   RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.FullControl))
            {
                approvedKey.SetValue($"{{{typeof(ThumbnailProvider).GUID.ToString().ToUpper()}}}", nameof(ThumbnailProvider));
            }
        }

        private static void SetDisableProcessIsolationValue(int disableProcessIsolationValue)
        {
            using (RegistryKey serverKey = Registry.ClassesRoot.OpenSubKey($"CLSID\\{{{typeof(ThumbnailProvider).GUID.ToString().ToUpper()}}}",
                   RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.FullControl))
            {
                serverKey.SetValue("DisableProcessIsolation", disableProcessIsolationValue);
            }
        }

        private static void AddThumbnailProviderToRegistry(string storageProviderName)
        {
            using (RegistryKey rootSyncManagers = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\SyncRootManager",
                RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.FullControl))
            {
                foreach (string subkey in rootSyncManagers.GetSubKeyNames())
                {
                    if (subkey.StartsWith(storageProviderName))
                    {
                        using (RegistryKey syncManager = rootSyncManagers.OpenSubKey(subkey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.FullControl))
                        {
                            syncManager.SetValue("ThumbnailProvider", $"{{{typeof(ThumbnailProvider).GUID.ToString().ToUpper()}}}");
                        }
                    }
                }
            }
        }

        private static void RemoveThumbnailProviderFromRegistry(string storageProviderName)
        {
            using (RegistryKey rootSyncManagers = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\SyncRootManager",
                   RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.FullControl))
            {
                foreach (string subkey in rootSyncManagers.GetSubKeyNames())
                {
                    if (subkey.StartsWith(storageProviderName))
                    {
                        using (RegistryKey syncManager = rootSyncManagers.OpenSubKey(subkey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.FullControl))
                        {
                            syncManager.DeleteValue("ThumbnailProvider");
                        }
                    }
                }
            }
        }

    }
}
