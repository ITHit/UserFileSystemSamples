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
        /// This method does not sync moved and deleted items. 
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
                    await engine.ClientNotifications(userFileSystemPath, this).CreateOrUpdateAsync();
                }
                catch (ClientLockFailedException ex)
                {
                    // Blocked for create/update/lock/unlock operation from another thread.
                    // Thrown by CreateAsync()/UpdateAsync() call. This is a normal behaviour.
                    LogMessage(ex.Message, ex.Path);
                }
                catch (Exception ex)
                {
                    LogError("Creation or update failed", userFileSystemPath, null, ex);
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
                    LogError("Folder sync failed", userFileSystemPath, null, ex);
                }
            }
        }
    }
}
