using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.Provider;

using log4net;

using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Windows.Package;
using ITHit.FileSystem.Windows.ShellExtension;
using System.Reflection;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Registers and unregisters sync root.
    /// </summary>
    public class Registrar
    {
        protected readonly string SyncRootId;
        protected readonly string UserFileSystemRootPath;
        protected readonly ILog Log;
        private readonly IEnumerable<(string Name, Guid Guid, bool AlwaysRegister)> shellExtensionHandlers;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="syncRootId">ID of the sync root.</param>
        /// <param name="userFileSystemRootPath">
        /// A root folder of your user file system. Your file system tree will be located under this folder.
        /// </param>
        /// <param name="log">log4net logger.</param>
        /// <param name="shellExtensionHandlers">
        /// List of shell extension handlers. Use it only for applications without application or package identity.
        /// For applications with identity this list is ignored.
        /// </param>
        public Registrar(string syncRootId, string userFileSystemRootPath, ILog log, IEnumerable<(string Name, Guid Guid, bool AlwaysRegister)> shellExtensionHandlers = null)
        {
            this.SyncRootId = syncRootId;
            this.UserFileSystemRootPath = userFileSystemRootPath;
            this.Log = log;
            this.shellExtensionHandlers = shellExtensionHandlers;
        }

        /// <summary>
        /// Registers sync root and creates application folders.
        /// </summary>
        /// <param name="displayName">Human readable display name.</param>
        /// <param name="iconPath">Path to the drive ico file.</param>
        /// <param name="shellExtensionsComServerExePath">Absolute path of external COM server executable to use when running without application identity. If not provided, will use current process.</param>
        /// <remarks>
        /// In the case of a packaged installer (msix) call this method during first program start.
        /// In the case of a regular installer (msi) call this method during installation.
        /// </remarks>
        public async Task RegisterSyncRootAsync(string displayName, string iconPath, string shellExtensionsComServerExePath = null)
        {
            if (!await IsRegisteredAsync(UserFileSystemRootPath))
            {
                Log.Info($"\n\nRegistering sync root.");
                Directory.CreateDirectory(UserFileSystemRootPath);

                await RegisterAsync(SyncRootId, UserFileSystemRootPath, displayName, iconPath);
            }
            else
            {
                Log.Info($"\n\nSync root already registered: {UserFileSystemRootPath}");
            }

            if (shellExtensionHandlers != null)
            {
                // Register thumbnails handler, custom states handler, etc.
                RegisterShellExtensions(shellExtensionsComServerExePath);
            }
        }

        /// <summary>
        /// Unregisters sync root, shell extensions, deletes all synced items. 
        /// </summary>
        /// <remarks>Call the code below during programm uninstall using classic msi.</remarks>
        public virtual async Task UnregisterSyncRootAsync(EngineWindows engine)
        {
            // Unregister sync root.
            if (await IsRegisteredAsync(UserFileSystemRootPath))
            {
                Log.Info($"\n\nUnregistering sync root.");
                await UnregisterAsync(SyncRootId);
            }
            else
            {
                Log.Info($"\n\n{UserFileSystemRootPath} sync root already unregistered.");
            }

            // Unregister shell extensions.
            if (shellExtensionHandlers != null)
            {
                UnregisterShellExtensions();
            }

            // Delete all files/folders.
            await CleanupAppFoldersAsync(engine);
        }

        public async Task CleanupAppFoldersAsync(EngineWindows engine)
        {
            Log.Info("\n\nDeleting all file and folder placeholders.");
            try
            {
                if (Directory.Exists(UserFileSystemRootPath))
                {
                    Directory.Delete(UserFileSystemRootPath, true);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to delete placeholders.", ex);
            }

            try
            {
                if (engine != null)
                {
                    await engine.UninstallCleanupAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"\n{ex}");
            }
        }

        /// <summary>
        /// Unregisters all components.
        /// </summary>
        /// <param name="fullUninstall">
        /// Pass true in the released application to remove all registered components.
        /// Pass false in development mode, to keep sparse package, 
        /// development certificate or any other components required for development convenience.
        /// </param>
        public virtual async Task UnregisterAsync(EngineWindows engine, bool fullUnregistration = true)
        {
            await UnregisterSyncRootAsync(engine);
        }

        /// <summary>
        /// Registers shell service providers COM classes as well as registers them for sync root.
        /// Use this method only for applications without application or package identity.
        /// </summary>
        /// <param name="syncRootId">Sync root identifier.</param>
        /// <param name="shellExtensionHandlers">List of shell extension handlers.</param>
        /// <param name="log">log4net Logger.</param>
        /// <param name="shellExtensionsComServerExePath">Absolute path of external COM server executable. If not provided, will use current process.</param>
        /// <remarks>
        /// Note that this method can NOT register context menu commands on Windows 11. Windows 11 context menu
        /// requires application or package identity.
        /// </remarks>
        private void RegisterShellExtensions(string shellExtensionsComServerExePath = null)
        {
            bool isRunningWithIdentity = PackageRegistrar.IsRunningWithIdentity();
            foreach (var handler in shellExtensionHandlers)
            {
                if (!ShellExtensionRegistrar.IsHandlerRegistered(handler.Guid))
                {
                    // Register handlers only if this app has no identity. Otherwise manifest will do this automatically.
                    // Unlike other handlers, CopyHook requires registration regardless of identity.
                    if (!isRunningWithIdentity || handler.AlwaysRegister)
                    {
                        Log.Info($"\nRegistering shell extension {handler.Name} with CLSID {handler.Guid:B}...\n");
                        ShellExtensionRegistrar.RegisterHandler(SyncRootId, handler.Name, handler.Guid, shellExtensionsComServerExePath);
                    }
                }
            }
        }

        /// <summary>
        /// Unregisters shell service providers COM classes.
        /// Use this method only for applications without application or package identity.
        /// </summary>
        /// <param name="shellextensionHandlers">List of shell extension handlers.</param>
        /// <param name="log">log4net Logger.</param>
        /// <remarks>
        /// Note that this method can NOT unregister context menu commands on Windows 11. Windows 11 context menu 
        /// requires application or package identity.
        /// </remarks>
        private void UnregisterShellExtensions()
        {
            foreach (var handler in shellExtensionHandlers)
            {
                if (ShellExtensionRegistrar.IsHandlerRegistered(handler.Guid))
                {
                    Log.Info($"\nUnregistering shell extension {handler.Name} with CLSID {handler.Guid:B}...\n");

                    ShellExtensionRegistrar.UnregisterHandler(handler.Guid);
                }
            }
        }

        
        /// <summary>
        /// Registers sync root.
        /// </summary>
        /// <param name="syncRootId">ID of the sync root.</param>
        /// <param name="path">A root folder of your user file system. Your file system tree will be located under this folder.</param>
        /// <param name="displayName">Human readable display name.</param>
        /// <param name="iconPath">Path to the drive ico file.</param>
        /// <remarks>
        /// In the case of a packaged installer (msix) call this method during first program start.
        /// In the case of a regular installer (msi) call this method during installation.
        /// </remarks>
        private static async Task RegisterAsync(string syncRootId, string path, string displayName, string iconPath)
        {
            StorageProviderSyncRootInfo storageInfo = new StorageProviderSyncRootInfo();
            storageInfo.Path = await StorageFolder.GetFolderFromPathAsync(path);
            storageInfo.Id = syncRootId;
            storageInfo.DisplayNameResource = displayName;
            storageInfo.IconResource = iconPath;
            storageInfo.Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            storageInfo.RecycleBinUri = new Uri("https://userfilesystem.com/recyclebin");
            storageInfo.Context = CryptographicBuffer.ConvertStringToBinary(path, BinaryStringEncoding.Utf8);

            // To open mp4 files using Windows Movies & TV application the Hydration Policy must be set to Full.
            storageInfo.HydrationPolicy = StorageProviderHydrationPolicy.Full;
            storageInfo.HydrationPolicyModifier = StorageProviderHydrationPolicyModifier.AutoDehydrationAllowed; //| StorageProviderHydrationPolicyModifier.ValidationRequired;

            // To implement folders on-demand placeholders population set the 
            // StorageProviderSyncRootInfo.PopulationPolicy to StorageProviderPopulationPolicy.Full.
            storageInfo.PopulationPolicy = StorageProviderPopulationPolicy.Full; // Set to Full to list folder content immediately on program start.

            // The read-only attribute is used to indicate that the item is being locked by another user. Do NOT include it into InSyncPolicy.
            storageInfo.InSyncPolicy = 
                //StorageProviderInSyncPolicy.FileCreationTime    | StorageProviderInSyncPolicy.DirectoryCreationTime |
                //StorageProviderInSyncPolicy.FileLastWriteTime   | StorageProviderInSyncPolicy.DirectoryLastWriteTime |
                //StorageProviderInSyncPolicy.FileHiddenAttribute | StorageProviderInSyncPolicy.DirectoryHiddenAttribute |
                //StorageProviderInSyncPolicy.FileSystemAttribute | StorageProviderInSyncPolicy.DirectorySystemAttribute |
                //StorageProviderInSyncPolicy.FileReadOnlyAttribute | StorageProviderInSyncPolicy.DirectoryReadOnlyAttribute |
                StorageProviderInSyncPolicy.Default;

            //storageInfo.ShowSiblingsAsGroup = false;
            //storageInfo.HardlinkPolicy = StorageProviderHardlinkPolicy.None;


            // Adds columns to Windows File Manager. 
            // Show/hide columns in the "More..." context menu on the columns header in Windows Explorer.
            var proDefinitions = storageInfo.StorageProviderItemPropertyDefinitions;
            proDefinitions.Add(new StorageProviderItemPropertyDefinition { DisplayNameResource = "Lock Owner"   , Id = (int)CustomColumnIds.LockOwnerIcon });            
            proDefinitions.Add(new StorageProviderItemPropertyDefinition { DisplayNameResource = "Lock Scope"   , Id = (int)CustomColumnIds.LockScope });
            proDefinitions.Add(new StorageProviderItemPropertyDefinition { DisplayNameResource = "Lock Expires" , Id = (int)CustomColumnIds.LockExpirationDate });            
            proDefinitions.Add(new StorageProviderItemPropertyDefinition { DisplayNameResource = "ETag"         , Id = (int)CustomColumnIds.ETag });


            ValidateStorageProviderSyncRootInfo(storageInfo);

            StorageProviderSyncRootManager.Register(storageInfo);
        }
        

        /// <summary>
        /// Ensures that minimum required properties for <see cref="StorageProviderSyncRootInfo"/> are set.
        /// </summary>
        private static void ValidateStorageProviderSyncRootInfo(StorageProviderSyncRootInfo info)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            string message = "Property not set.";

            // DisplayNameResource is required.
            if (string.IsNullOrEmpty(info.Id))
            {
                throw new ArgumentException(message, "StorageProviderSyncRootInfo.Id");
            }

            // Version is required. Otherwise StorageProviderSyncRootManager.Register() call hangs.
            if (string.IsNullOrEmpty(info.Version))
            {
                throw new ArgumentException(message, "StorageProviderSyncRootInfo.Version");
            }

            // DisplayNameResource is required.
            if (string.IsNullOrEmpty(info.DisplayNameResource))
            {
                throw new ArgumentException(message, "StorageProviderSyncRootInfo.DisplayNameResource");
            }

            // IconResource is required.
            if (string.IsNullOrEmpty(info.IconResource))
            {
                throw new ArgumentException(message, "StorageProviderSyncRootInfo.IconResource");
            }

            // RecycleBinUri is required.
            if (info.RecycleBinUri == null)
            {
                throw new ArgumentException(message, "StorageProviderSyncRootInfo.RecycleBinUri");
            }

            // Context is required.
            if (info.Context == null)
            {
                throw new ArgumentException(message, "StorageProviderSyncRootInfo.Context");
            }
        }

        /// <summary>
        /// Determines if the syn root is registered for specified folder.
        /// </summary>
        /// <param name="path">Sync root path.</param>
        /// <returns>True if the sync root is registered, false otherwise.</returns>
        private static async Task<bool> IsRegisteredAsync(string path)
        {
            if (Directory.Exists(path))
            {
                StorageFolder storageFolder = await StorageFolder.GetFolderFromPathAsync(path);
                try
                {
                    StorageProviderSyncRootManager.GetSyncRootInformationForFolder(storageFolder);
                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Unregisters sync root.
        /// </summary>
        /// <param name="syncRootId">ID of the sync root.</param>
        /// <remarks>
        /// <para>
        /// In the case of a packaged installer (msix) you do not need to call this method. 
        /// The platform will automatically delete sync root registartion during program uninstall.
        /// </para>
        /// <para>
        /// In the case of a regular installer (msi) call this method during uninstall.
        /// </para>
        /// <para>
        /// All file and folder placeholders are converted into regular files/folders during this call. 
        /// </para>
        /// </remarks>
        private static async Task UnregisterAsync(string syncRootId)
        {
            StorageProviderSyncRootManager.Unregister(syncRootId);
        }
    }
}
