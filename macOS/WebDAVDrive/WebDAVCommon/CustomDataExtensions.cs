using System.IO;
using System.Text;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;

namespace WebDAVCommon
{
    /// <summary>
    /// Provides methods for getting and setting custom data associated with a file or folder.
    /// </summary>
    public static class CustomDataExtensions
    {
        /// <summary>
        /// Tries to delete lock info.
        /// </summary>
        /// <param name="placeholder">User file system placeholder item.</param>
        /// <returns>True if method succeeded. False - otherwise.</returns>
        public static bool TryDeleteLockInfo(this ICustomData properties)
        {
            try
            {
                properties.TryGetValue("Identifier", out IDataItem dataItem);
                string Identifier = dataItem.GetValue<string>();

                //Identifier = ItemIdentifierUtil.Encode(identifier, isDirectory);
                //string identifier = Encoding.UTF8.GetString(RemoteStorageItemId);
                string identifier = Identifier;
                SqliteStorageManager.GetInstance().RemoveLockToken(identifier);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
