using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Represents a file or a folder on a virtul drive.
    /// </summary>
    public interface IVirtualFileSystemItem
    {
        /// <summary>
        /// Renames or moves this file or folder to a new location in the remote storage.
        /// </summary>
        /// <param name="userFileSystemNewPath">Target path of this file or folder in the user file system.</param>
        Task MoveToAsync(string userFileSystemNewPath);

        /// <summary>
        /// Deletes this file or folder in the remote storage.
        /// </summary>
        Task DeleteAsync();
    }
}