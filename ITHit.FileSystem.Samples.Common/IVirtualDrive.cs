using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Represents a virtual drive. Processes OS file system calls, 
    /// synchronizes user file system to remote storage and back, 
    /// monitors files pinning and unpinning.
    /// </summary>
    /// <remarks>
    /// This class calls <see cref="IVirtualFile"/> and <see cref="IVirtualFolder"/> interfaces returned from 
    /// the <see cref="GetVirtualFileSystemItemAsync(string, FileSystemItemTypesEnum)"/> fectory method. It also provides methods for updating 
    /// the user file system in response to notifications sent by the remote storage.
    /// </remarks>
    public interface IVirtualDrive
    {
        /// <summary>
        /// Gets a file or a folder item corresponding to path in the user file system.
        /// </summary>
        /// <param name="userFileSystemPath">
        /// Path in user file system for which your will return a file or a folder.
        /// </param>
        /// <param name="itemType">Type of the item to return.</param>
        /// <param name="logger">Logger.</param>
        /// <remarks>
        /// <para>
        /// This is a factory method that returns files and folders in your remote storage.
        /// From this method implementation you will return a file or a folder item that corresponds 
        /// to provided <paramref name="userFileSystemPath"/> parameter and type of item - <paramref name="itemType"/>.
        /// Your files must implement <see cref="IVirtualFile"/> interface. Your folders must implement <see cref="IVirtualFolder"/> interface.
        /// </para>
        /// <para>
        /// The Engine will then call <see cref="IVirtualFile"/> and <see cref="IVirtualFolder"/> methods to get the 
        /// required information and pass it to the platform.
        /// </para>
        /// <para>
        /// Note that this method may be called for files that does not exist in the user file system, 
        /// for example for files that were moved in user file system when the application was not running.
        /// </para>
        /// </remarks>
        /// <returns>A file or a folder item that corresponds to the path in the user file system.</returns>
        Task<IVirtualFileSystemItem> GetVirtualFileSystemItemAsync(string userFileSystemPath, FileSystemItemTypeEnum itemType, ILogger logger);

        /// <summary>
        /// Current synchronization state of this virtual drive.
        /// </summary>
        SynchronizationState SyncState { get; }

        /// <summary>
        /// Event, fired when synchronization state changes.
        /// </summary>
        event SyncronizationEvent SyncEvent;

        /// <summary>
        /// Enables or disables full synchronization service and user file sytem monitor.
        /// </summary>
        /// <param name="enabled">Pass true to start synchronization. Pass false - to stop.</param>
        Task SetEnabledAsync(bool enabled);

        /// <summary>
        /// Use object returned by this method to send messages to the remote storage, 
        /// such as lock and unlock commands.
        /// </summary>
        /// <param name="userFileSystemPath">Path in the user file system to send a notification about.</param>
        /// <remarks>
        /// Call methods of the object returned by this method when the client needs 
        /// to notify the remote storage about the changes made in user file system.
        /// </remarks>
        IClientNotifications ClientNotifications(string userFileSystemPath, ILogger logger);
    }
}
