using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.Provider;

namespace VirtualFileSystem
{
    internal static class Registrar
    {
        /// <summary>
        /// Registers sync root.
        /// </summary>
        /// <param name="syncRootId">ID of the sync root.</param>
        /// <param name="path">A root folder of your user file system. Your file system tree will be located under this folder.</param>
        /// <param name="displayName">Human readable display name.</param>
        /// <remarks>Call this method during application installation.</remarks>
        public static async Task RegisterAsync(string syncRootId, string path, string displayName)
        {
            StorageProviderSyncRootInfo storageInfo = new StorageProviderSyncRootInfo();
            storageInfo.Path = await StorageFolder.GetFolderFromPathAsync(path);
            storageInfo.Id = syncRootId;
            storageInfo.DisplayNameResource = displayName;
            storageInfo.IconResource = "%SystemRoot%\\system32\\charmap.exe,0";
            storageInfo.Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            storageInfo.RecycleBinUri = new Uri("https://userfilesystem.com/recyclebin");
            storageInfo.Context = CryptographicBuffer.ConvertStringToBinary(path, BinaryStringEncoding.Utf8);

            storageInfo.HydrationPolicy = StorageProviderHydrationPolicy.Progressive;
            storageInfo.HydrationPolicyModifier = StorageProviderHydrationPolicyModifier.AutoDehydrationAllowed | StorageProviderHydrationPolicyModifier.ValidationRequired;

            // To implement folders on-demand placeholders population set the StorageProviderSyncRootInfo.PopulationPolicy to StorageProviderPopulationPolicy.Full.
            storageInfo.PopulationPolicy = StorageProviderPopulationPolicy.Full; // Set to Full to list folder content immediately on program start.
            storageInfo.InSyncPolicy = 
                StorageProviderInSyncPolicy.FileLastWriteTime |
                StorageProviderInSyncPolicy.FileCreationTime |
                StorageProviderInSyncPolicy.FileHiddenAttribute |
                StorageProviderInSyncPolicy.FileReadOnlyAttribute |
                StorageProviderInSyncPolicy.FileSystemAttribute |
                StorageProviderInSyncPolicy.DirectoryLastWriteTime |
                StorageProviderInSyncPolicy.DirectoryCreationTime |
                StorageProviderInSyncPolicy.DirectoryHiddenAttribute |
                StorageProviderInSyncPolicy.DirectoryReadOnlyAttribute |
                StorageProviderInSyncPolicy.DirectorySystemAttribute;

                
            //storageInfo.ShowSiblingsAsGroup = false;
            //storageInfo.HardlinkPolicy = StorageProviderHardlinkPolicy.None;

            /*
            var proDefinitions = storageInfo.StorageProviderItemPropertyDefinitions;
            proDefinitions.Add(new StorageProviderItemPropertyDefinition { DisplayNameResource = "Test0", Id = 0, });
            proDefinitions.Add(new StorageProviderItemPropertyDefinition { DisplayNameResource = "Test1", Id = 1, });
            proDefinitions.Add(new StorageProviderItemPropertyDefinition { DisplayNameResource = "Test2", Id = 2, });
            */

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
