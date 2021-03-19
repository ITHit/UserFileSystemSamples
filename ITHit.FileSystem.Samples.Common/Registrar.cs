using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.Provider;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Registers and unregisters sync root.
    /// </summary>
    public static class Registrar
    {
        /// <summary>
        /// Registers sync root.
        /// </summary>
        /// <param name="syncRootId">ID of the sync root.</param>
        /// <param name="path">A root folder of your user file system. Your file system tree will be located under this folder.</param>
        /// <param name="displayName">Human readable display name.</param>
        /// <param name="iconPath">Path to the drive ico file.</param>
        /// <remarks>Call this method during application installation.</remarks>
        public static async Task RegisterAsync(string syncRootId, string path, string displayName, string iconPath)
        {
            StorageProviderSyncRootInfo storageInfo = new StorageProviderSyncRootInfo();
            storageInfo.Path = await StorageFolder.GetFolderFromPathAsync(path);
            storageInfo.Id = syncRootId;
            storageInfo.DisplayNameResource = displayName;
            storageInfo.IconResource = iconPath;
            storageInfo.Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            storageInfo.RecycleBinUri = new Uri("https://userfilesystem.com/recyclebin");
            storageInfo.Context = CryptographicBuffer.ConvertStringToBinary(path, BinaryStringEncoding.Utf8);

            storageInfo.HydrationPolicy = StorageProviderHydrationPolicy.Progressive;
            storageInfo.HydrationPolicyModifier = StorageProviderHydrationPolicyModifier.AutoDehydrationAllowed | StorageProviderHydrationPolicyModifier.ValidationRequired;

            // To implement folders on-demand placeholders population set the 
            // StorageProviderSyncRootInfo.PopulationPolicy to StorageProviderPopulationPolicy.Full.
            storageInfo.PopulationPolicy = StorageProviderPopulationPolicy.Full; // Set to Full to list folder content immediately on program start.

            // The read-only attribute is used to indicate that the item is being locked by another user. Do not include it into InSyncPolicy.
            storageInfo.InSyncPolicy =
                StorageProviderInSyncPolicy.FileCreationTime    | StorageProviderInSyncPolicy.DirectoryCreationTime |
                StorageProviderInSyncPolicy.FileLastWriteTime   | StorageProviderInSyncPolicy.DirectoryLastWriteTime |
                StorageProviderInSyncPolicy.FileHiddenAttribute | StorageProviderInSyncPolicy.DirectoryHiddenAttribute |
                StorageProviderInSyncPolicy.FileSystemAttribute | StorageProviderInSyncPolicy.DirectorySystemAttribute;
            //StorageProviderInSyncPolicy.FileReadOnlyAttribute | StorageProviderInSyncPolicy.DirectoryReadOnlyAttribute;

            //storageInfo.ShowSiblingsAsGroup = false;
            //storageInfo.HardlinkPolicy = StorageProviderHardlinkPolicy.None;

            
            // Adds columns to Windows File Manager. 
            // Show/hide columns in the "More..." context menu on the columns header.
            var proDefinitions = storageInfo.StorageProviderItemPropertyDefinitions;
            proDefinitions.Add(new StorageProviderItemPropertyDefinition { DisplayNameResource = "Lock Owner"   , Id = 2 });            
            proDefinitions.Add(new StorageProviderItemPropertyDefinition { DisplayNameResource = "Lock Scope"   , Id = 3 });
            proDefinitions.Add(new StorageProviderItemPropertyDefinition { DisplayNameResource = "Lock Expires" , Id = 4 });            
            proDefinitions.Add(new StorageProviderItemPropertyDefinition { DisplayNameResource = "ETag"         , Id = 5 });


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
        /// Determins if the syn root is registered for specified folder.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>True if the sync root is registered, false otherwise.</returns>
        public static async Task<bool> IsRegisteredAsync(string path)
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

            return false;
        }

        /// <summary>
        /// Unregisters sync root.
        /// </summary>
        /// <param name="syncRootId">ID of the sync root.</param>
        /// <remarks>
        /// Call this method during application ununstall. 
        /// All file and folder placeholders are converted into regular files/folders during this call. 
        /// </remarks>
        public static async Task UnregisterAsync(string syncRootId)
        {
            StorageProviderSyncRootManager.Unregister(syncRootId);
        }
    }
}
