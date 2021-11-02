using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    public interface IMapping
    {
        /// <summary>
        /// Returns a remote storage URI that corresponds to the user file system path.
        /// </summary>
        /// <param name="userFileSystemPath">Full path in the user file system.</param>
        /// <returns>Remote storage URI that corresponds to the <paramref name="userFileSystemPath"/>.</returns>
        //string MapPath(string userFileSystemPath);

        /// <summary>
        /// Returns a user file system path that corresponds to the remote storage URI.
        /// </summary>
        /// <param name="remoteStorageUri">Remote storage URI.</param>
        /// <returns>Path in the user file system that corresponds to the <paramref name="remoteStorageUri"/>.</returns>
        //string ReverseMapPath(string remoteStorageUri);

        /// <summary>
        /// Returns a user file system item info from the remote storage URI making a request to the remote storage.
        /// </summary>
        /// <param name="remoteStorageItem">Remote storage URI.</param>
        /// <returns>User file system item info.</returns>
        //Task<FileSystemItemMetadataExt> ReverseMapItemAsync(string remoteStorageUri);

        /// <summary>
        /// Returns true if the remote item is modified. False - otherwise.
        /// </summary>
        /// <param name="userFileSystemPath">User file system path.</param>
        /// <param name="remoteStorageItem">Remote storage item metadata.</param>
        /// <returns></returns>
        Task<bool> IsModifiedAsync(string userFileSystemPath, FileSystemItemMetadataExt remoteStorageItemMetadata, ILogger logger);
    }
}
