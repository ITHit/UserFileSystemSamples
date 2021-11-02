using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// User File System to Remote Storage synchronization.
    /// </summary>
    /// <remarks>In most cases you can use this class in your project without any changes.</remarks>
    internal class ClientToServerSync : Logger
    {
        /// <summary>
        /// Virtual drive.
        /// </summary>
        private readonly VirtualEngineBase engine;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="log">Logger.</param>
        internal ClientToServerSync(VirtualEngineBase engine, ILog log) : base("UFS -> RS Sync", log)
        {
            this.engine = engine;
        }

        /// <summary>
        /// Recursively updates and creates files and folders in the remote storage. 
        /// </summary>
        /// <param name="userFileSystemFolderPath">Folder path in the user file system.</param>
        /// <remarks>
        /// Synchronizes only folders already loaded into the user file system.
        /// This method does not sync moved and deleted files. 
        /// </remarks>
        internal async Task SyncronizeFolderAsync(string userFileSystemFolderPath)
        {
            // In case of on-demand loading the user file system contains only a subset of the server files and folders.
            // Here we sync folder only if its content already loaded into user file system (folder is not offline).
            // The folder content is loaded inside IFolder.GetChildrenAsync() method.
            if (new DirectoryInfo(userFileSystemFolderPath).Attributes.HasFlag(System.IO.FileAttributes.Offline))
            {
//                LogMessage("Folder offline, skipping:", userFileSystemFolderPath);
                return;
            }

            IEnumerable<string> userFileSystemChildren = Directory.EnumerateFileSystemEntries(userFileSystemFolderPath, "*");
//            LogMessage("Synchronizing:", userFileSystemFolderPath);


            // Update and create files/folders in remote storage.
            foreach (string userFileSystemPath in userFileSystemChildren)
            {
                try
                {
                    await CreateOrUpdateAsync(userFileSystemPath, engine, this);
                }
                catch (Exception ex)
                {
                    LogError("Update failed", userFileSystemPath, null, ex);
                }

                // Synchronize subfolders.
                try
                {                    
                    if (Directory.Exists(userFileSystemPath))
                    {
                        await SyncronizeFolderAsync(userFileSystemPath);
                    }
                }
                catch (Exception ex)
                {
                    LogError("Folder sync failed:", userFileSystemPath, null, ex);
                }
            }
        }
        
        /// <summary>
        /// Creates the item in the remote storate if the item is new. 
        /// Updates the item in the remote storage if the item in not new.
        /// </summary>
        /// <param name="userFileSystemPath">File or folder path the user file system. This can be a placeholder or a regular file/folder.</param>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger instance.</param>
        internal static async Task CreateOrUpdateAsync(string userFileSystemPath, VirtualEngineBase engine, ILogger logger)
        {
            if (System.IO.File.Exists(userFileSystemPath)
                && !FilterHelper.AvoidSync(userFileSystemPath))
            {
                if (engine.ExternalDataManager(userFileSystemPath, logger).IsNew)
                {
                    if (!PlaceholderItem.IsPlaceholder(userFileSystemPath))
                    {
                        // New file/folder, creating new item in the remote storage.
                        await engine.ClientNotifications(userFileSystemPath, logger).CreateAsync();
                    }
                }
                else
                {
                    if (!PlaceholderItem.IsPlaceholder(userFileSystemPath))
                    {
                        // The item was converted to a regular file during MS Office or AutoCAD transactiona save,
                        // converting it back to placeholder and uploading to the remote storage.

                        //if (!AutoCadFilterHelper.IsAutoCadLocked(userFileSystemPath))
                        //{
                            logger.LogMessage("Converting to placeholder", userFileSystemPath);
                            PlaceholderItem.ConvertToPlaceholder(userFileSystemPath, null, null, false);
                            await engine.ClientNotifications(userFileSystemPath, logger).UpdateAsync();
                            await engine.ExternalDataManager(userFileSystemPath).RefreshCustomColumnsAsync();
                        //}
                    }
                    else if (!PlaceholderItem.GetItem(userFileSystemPath).GetInSync())
                    {
                        // The item is modified in the user file system, uploading to the remote storage.
                        await engine.ClientNotifications(userFileSystemPath, logger).UpdateAsync();
                        await engine.ExternalDataManager(userFileSystemPath).RefreshCustomColumnsAsync();
                    }
                }
            }
        }
        
    }
}
