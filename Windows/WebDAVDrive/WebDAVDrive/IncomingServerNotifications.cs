using System.IO;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;

namespace WebDAVDrive
{
    /// <summary>
    /// Provides custom properties storage and logging for incoming updates from remote storage.
    /// </summary>
    internal class IncomingServerNotifications
    {
        /// <summary>
        /// Virtual drive.
        /// </summary>
        protected readonly VirtualEngine Engine;

        /// <summary>
        /// Logger.
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        internal IncomingServerNotifications(VirtualEngine engine, ILogger logger)
        {
            this.Engine = engine;
            this.Logger = logger;
        }

        protected async Task IncomingCreatedAsync(string userFileSystemParentPath, FileSystemItemMetadataExt remoteStorageItem)
        {
            string userFileSystemPath = Path.Combine(userFileSystemParentPath, remoteStorageItem.Name);

            if (await Engine.ServerNotifications(userFileSystemParentPath, Logger).CreateAsync(new[] { remoteStorageItem }) > 0)
            {
                await Engine.Placeholders.GetItem(userFileSystemPath).SavePropertiesAsync(remoteStorageItem);

                // Because of the on-demand population, the parent folder placeholder may not exist in the user file system
                // or the folder may be offline. In this case the IServerNotifications.CreateAsync() call is ignored.
                Logger.LogMessage($"Created successfully", userFileSystemPath);
            }
        }

        protected async Task IncomingChangedAsync(string userFileSystemPath, FileSystemItemMetadataExt itemMetadata)
        {
            await Engine.Placeholders.GetItem(userFileSystemPath).SavePropertiesAsync(itemMetadata);

            // Can not update read-only files, read-only attribute must be removed.
            FileInfo userFileSystemFile = new FileInfo(userFileSystemPath);
            bool isReadOnly = userFileSystemFile.IsReadOnly;
            if (isReadOnly)
            {
                userFileSystemFile.IsReadOnly = false;
            }

            if (await Engine.ServerNotifications(userFileSystemPath, Logger).UpdateAsync(itemMetadata))
            {
                PlaceholderItem.UpdateUI(userFileSystemPath);
                Logger.LogMessage("Updated successfully", userFileSystemPath);
            }

            // Restore the read-only attribute.
            userFileSystemFile.IsReadOnly = isReadOnly;
        }

        protected async Task IncomingDeletedAsync(string userFileSystemPath)
        {
            if (await Engine.ServerNotifications(userFileSystemPath, Logger).DeleteAsync())
            {
                // Because of the on-demand population the file or folder placeholder may not exist in the user file system.
                // In this case the IServerNotifications.DeleteAsync() call is ignored.
                Logger.LogMessage("Deleted successfully", userFileSystemPath);
            }
        }
    }
}
