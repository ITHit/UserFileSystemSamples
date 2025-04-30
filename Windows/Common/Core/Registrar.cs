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
using System.Linq;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Registers and unregisters sync root.
    /// </summary>
    public class Registrar
    {
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
        public Registrar(ILog log, IEnumerable<(string Name, Guid Guid, bool AlwaysRegister)> shellExtensionHandlers = null)
        {
            this.Log = log;
            this.shellExtensionHandlers = shellExtensionHandlers;
        }

        /// <summary>
        /// Registers sync root and creates application folders.
        /// </summary>
        /// <param name="displayName">Human readable display name.</param>
        /// <param name="iconPath">Path to the drive ico file.</param>
        /// <param name="customColumns">
        /// A dictionary where the key represents the column ID (an integer) and the value represents the display name of the column (a string).
        /// These columns will appear in Windows File Explorer and can display additional metadata for files and folders, 
        /// such as lock owner, lock expiration date, or custom identifiers.
        /// </param>
        /// <remarks>
        /// In the case of a packaged installer (msix) call this method during first program start.
        /// In the case of a regular installer (msi) call this method during installation.
        /// </remarks>
        public async Task<StorageProviderSyncRootInfo> RegisterSyncRootAsync(string syncRootId, string userFileSystemRootPath, string remotestorageRootPath, string displayName, string iconPath, Dictionary<int, string>? customColumns)
        {
            StorageProviderSyncRootInfo syncRoot = null;
            if (!await IsRegisteredAsync(userFileSystemRootPath))
            {
                Log.Info($"\n\nRegistering sync root.");
                Directory.CreateDirectory(userFileSystemRootPath);

                syncRoot = await RegisterAsync(syncRootId, userFileSystemRootPath, remotestorageRootPath, displayName, iconPath, customColumns);
            }
            else
            {
                Log.Info($"\n\nSync root already registered: {userFileSystemRootPath}");
            }

            if (shellExtensionHandlers != null)
            {
                // Register thumbnails handler, custom states handler, etc.
                RegisterShellExtensions(syncRootId, shellExtensionHandlers, Log);
            }

            return syncRoot;
        }

        /*
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
        */

        /// <summary>
        /// Unmounts sync root, deletes all synced items and any data stored by the Engine.
        /// </summary>
        public static async Task<bool> UnregisterSyncRootAsync(string syncRootPath, string dataPath, ILog log)
        {
            bool res = await UnregisterSyncRootAsync(syncRootPath, log);

            // Delete data folder.

            // IMPORTANT!
            // Delete any data only if unregistration was succesefull!

            if (res && !string.IsNullOrWhiteSpace(dataPath))
            {
                log.Debug($"Deleteing data folder for {syncRootPath}");
                try
                {
                    Directory.Delete(dataPath, true);
                }
                catch (Exception ex)
                {
                    res = false;
                    log.Error($"Failed to delete data folder {syncRootPath} {dataPath}", ex);
                }
            }

            return res;
        }

        /// <summary>
        /// Unmounts sync root, deletes all synced items. 
        /// </summary>
        private static async Task<bool> UnregisterSyncRootAsync(string syncRootPath, ILog logger)
        {
            bool res = false;
            // Get sync root ID.
            StorageFolder storageFolder = await StorageFolder.GetFolderFromPathAsync(syncRootPath);
            StorageProviderSyncRootInfo syncRootInfo = null;
            try
            {
                logger.Debug($"Getting sync root info {syncRootPath}");
                syncRootInfo = StorageProviderSyncRootManager.GetSyncRootInformationForFolder(storageFolder);
            }
            catch (Exception ex)
            {
                logger.Error($"Sync root is not registered {syncRootPath}", ex);
            }

            // Unregister sync root.
            if (syncRootInfo != null)
            {
                try
                { 
                    logger.Debug($"Unregistering sync root {syncRootPath} {syncRootInfo.Id}");
                    StorageProviderSyncRootManager.Unregister(syncRootInfo.Id);
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to unregister sync root {syncRootPath} {syncRootInfo.Id}", ex);
                    // IMPORTANT!
                    // If Unregister() failed, deleting items on the client may trigger deleting
                    // items in the remote storage if the Engine did not stop or if started again.
                    // Do NOT delete sync root folder in this case!
                    return res;
                }
            }

            // Remore the read-only arrtibute. Otherwise delete fails.
            var allItems = Directory.EnumerateFileSystemEntries(syncRootPath, "*", SearchOption.AllDirectories);
            foreach(var path in allItems)
            {
                try
                {
                    new FileInfo(path).IsReadOnly = false;
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to remove read-only attribute for {path}", ex);
                }
            }

            // Delete sync root folder.
            try
            {
                logger.Debug($"Deleteing sync root folder {syncRootPath}");
                Directory.Delete(syncRootPath, true);
                res = true;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to delete sync root folder {syncRootPath}", ex);
            }
            return res;
        }

        /// <summary>
        /// Unregisters all sync roots that has a provider ID and removes all components.
        /// </summary>
        /// <param name="providerId">This method will only unmount sync roots that has this provider ID.</param>
        /// <param name="fullUnregistration">
        /// Pass true in the released application to remove all registered components.
        /// Pass false in development mode, to keep sparse package, 
        /// development certificate or any other components required for development convenience.
        /// </param>
        public virtual async Task<bool> UnregisterAllSyncRootsAsync(string providerId, bool fullUnregistration = true)
        {
            bool res = true;
            var syncRoots = StorageProviderSyncRootManager.GetCurrentSyncRoots();
            foreach(var syncRoot in syncRoots)
            {
                string storedProviderId = syncRoot.Id?.Split('!')?.FirstOrDefault();
                if (storedProviderId.Equals(providerId))
                {
                    if (!await UnregisterSyncRootAsync(syncRoot.Path.Path, Log))
                    {
                        res = false;
                    }
                }
            }

            // Unregister shell extensions.
            if (shellExtensionHandlers != null)
            {
                UnregisterShellExtensions();
            }

            return res;
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
        private static void RegisterShellExtensions(string syncRootId, IEnumerable<(string Name, Guid Guid, bool AlwaysRegister)> shellExtensionHandlers, ILog log, string shellExtensionsComServerExePath = null)
        {
            bool isRunningWithIdentity = PackageRegistrar.IsRunningWithIdentity();
            foreach (var handler in shellExtensionHandlers)
            {
                //if (!ShellExtensionRegistrar.IsHandlerRegistered(handler.Guid))
                {
                    // Register handlers only if this app has no identity. Otherwise manifest will do this automatically.
                    // Unlike other handlers, CopyHook requires registration regardless of identity.
                    if (!isRunningWithIdentity || handler.AlwaysRegister)
                    {
                        log.Info($"\nRegistering shell extension {handler.Name} with CLSID {handler.Guid:B}...\n");
                        ShellExtensionRegistrar.RegisterHandler(syncRootId, handler.Name, handler.Guid, shellExtensionsComServerExePath);
                    }
                }
            }
        }

        /// <summary>
        /// Unregisters shell service providers COM classes.
        /// Use this method only for applications without application or package identity.
        /// </summary>
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
        /// <param name="remoteStoragePath">Remote storage path. It will be stored inide the sync root to distinguish between sync roots when mounting a new remote storage.</param>
        /// <param name="displayName">Human readable display name.</param>
        /// <param name="iconPath">Path to the drive ico file.</param>
        /// <param name="customColumns">
        /// A dictionary where the key represents the column ID (an integer) and the value represents the display name of the column (a string).
        /// These columns will appear in Windows File Explorer and can display additional metadata for files and folders, 
        /// such as lock owner, lock expiration date, or custom identifiers.
        /// </param>
        /// <remarks>
        /// In the case of a packaged installer (msix) call this method during first program start.
        /// In the case of a regular installer (msi) call this method during installation.
        /// </remarks>
        private static async Task<StorageProviderSyncRootInfo> RegisterAsync(string syncRootId, string path, string remoteStoragePath, string displayName, string iconPath, Dictionary<int, string>? customColumns)
        {
            StorageProviderSyncRootInfo storageInfo = new StorageProviderSyncRootInfo();
            storageInfo.Path = await StorageFolder.GetFolderFromPathAsync(path);
            storageInfo.Id = syncRootId;
            storageInfo.DisplayNameResource = displayName;
            storageInfo.IconResource = iconPath;
            storageInfo.Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            storageInfo.RecycleBinUri = new Uri("https://userfilesystem.com/recyclebin");
            storageInfo.SetRemoteStoragePath(remoteStoragePath);
            //storageInfo.ProviderId = providerID; // Provider ID is not returned by StorageProviderSyncRootManager.GetCurrentSyncRoots()

            // To open mp4 files using Windows Movies & TV application the Hydration Policy must be set to Full.
            storageInfo.HydrationPolicy = StorageProviderHydrationPolicy.Full;
            storageInfo.HydrationPolicyModifier = StorageProviderHydrationPolicyModifier.AutoDehydrationAllowed; //| StorageProviderHydrationPolicyModifier.ValidationRequired;

            // To implement folders on-demand placeholders population set the 
            // StorageProviderSyncRootInfo.PopulationPolicy to StorageProviderPopulationPolicy.Full.
            storageInfo.PopulationPolicy = StorageProviderPopulationPolicy.Full; // Set to Full to list folder content immediately on program start.

            // The read-only attribute is used to indicate that the item is being locked by another user. Do NOT include it into InSyncPolicy.
            storageInfo.InSyncPolicy = StorageProviderInSyncPolicy.Default;

            // Adds columns to Windows File Manager. 
            // Show/hide columns in the "More..." context menu on the columns header in Windows Explorer.
            var proDefinitions = storageInfo.StorageProviderItemPropertyDefinitions;
            if(customColumns != null)
            {
                foreach (var column in customColumns)
                {
                    proDefinitions.Add(new StorageProviderItemPropertyDefinition { DisplayNameResource = column.Value, Id = column.Key });
                }
            }
      
            ValidateStorageProviderSyncRootInfo(storageInfo);

            StorageProviderSyncRootManager.Register(storageInfo);

            return storageInfo;
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
        /// <param name="userFileSystemPath">Sync root path.</param>
        /// <returns>True if the sync root is registered, false otherwise.</returns>
        private static async Task<bool> IsRegisteredAsync(string userFileSystemPath)
        {
            if (Directory.Exists(userFileSystemPath))
            {
                StorageFolder storageFolder = await StorageFolder.GetFolderFromPathAsync(userFileSystemPath);
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
        /// Determines if the syn root is registered for specified URI.
        /// </summary>
        /// <param name="uri">Uri.</param>
        /// <returns>True if the sync root is registered, false otherwise.</returns>
        public static bool IsRegisteredUri(Uri uri)
        {
            return GetSyncRootInfo(uri) != null;
        }

        /// <summary>
        /// Determines if the syn root is registered for specified URI.
        /// </summary>
        /// <param name="uri">Uri.</param>
        /// <returns>True if the sync root is registered, false otherwise.</returns>
        public static bool IsRegisteredUri(string uriString)
        {
            if (!Uri.IsWellFormedUriString(uriString, UriKind.Absolute))
                throw new ArgumentException("URI not well formed", nameof(uriString));

            if (Uri.TryCreate(uriString, UriKind.Absolute, out Uri uri))
            {
                return IsRegisteredUri(uri);
            }
            else
            {
                throw new ArgumentException("Can not create URI", nameof(uriString));
            }
        }

        /// <summary>
        /// Gets sync root info for specified URI or null if sync root is not registered for this URI.
        /// </summary>
        /// <param name="uri">Uri.</param>
        /// <returns>Sync root info or null.</returns>
        private static StorageProviderSyncRootInfo GetSyncRootInfo(Uri uri)
        {
            var syncRoots = StorageProviderSyncRootManager.GetCurrentSyncRoots();

            foreach (var syncRoot in syncRoots)
            {
                string storedUri = syncRoot.GetRemoteStoragePath();
                if (Uri.TryCreate(storedUri, UriKind.Absolute, out Uri storedParsedUri))
                {
                    if (storedParsedUri.Equals(uri))
                    {
                        return syncRoot;
                    }
                }    
            }

            return null;
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

        public static async Task<IEnumerable<StorageProviderSyncRootInfo>> GetMountedSyncRootsAsync(string providerId, ILog log)
        {
            _ = string.IsNullOrEmpty(providerId) ? throw new ArgumentNullException(nameof(providerId)) : string.Empty;

            IList<StorageProviderSyncRootInfo> mountedRoots = new List<StorageProviderSyncRootInfo>();
            IReadOnlyList<StorageProviderSyncRootInfo> syncRoots = StorageProviderSyncRootManager.GetCurrentSyncRoots();
            foreach (var syncRoot in syncRoots)
            {
                string storedProviderId = syncRoot.Id?.Split('!')?.FirstOrDefault();
                if (storedProviderId.Equals(providerId))
                {
                    string storedUri = syncRoot.GetRemoteStoragePath();
                    if (!System.Uri.TryCreate(storedUri, UriKind.Absolute, out System.Uri _))
                    {
                        log.Error($"Can not parse URI for {syncRoot.DisplayNameResource}: {storedUri}");
                        continue;
                    }

                    mountedRoots.Add(syncRoot);
                }
            }

            return mountedRoots;
        }
    }

    public static class StorageProviderSyncRootInfoExtensions
    {
        public static string GetRemoteStoragePath(this StorageProviderSyncRootInfo rootInfo)
        {
            return CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf16LE, rootInfo.Context);
        }

        public static void SetRemoteStoragePath(this StorageProviderSyncRootInfo rootInfo, string remoteStoragePath)
        {
            rootInfo.Context = CryptographicBuffer.ConvertStringToBinary(remoteStoragePath, BinaryStringEncoding.Utf16LE);
        }
    }
}
