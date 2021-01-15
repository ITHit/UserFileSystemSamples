using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace VirtualFileSystem
{
    /// <summary>
    /// Custom data stored with a file or folder placeholder, such original file/folder path. Max 4KB.
    /// </summary>
    /// <remarks>To avoid storing metatadata and keep footprit small, this class is is using custom serialization.</remarks>
    internal class CustomData
    {
        /// <summary>
        /// Keeps the original file/folder path. Used to sync file/folder from user file system to remote storage 
        /// if this app was not running when the file/folder was moved or renamed. This field allows to avoid 
        /// delete-create sequence during client to server synchronization after app failure.
        /// </summary>
        internal string OriginalPath = "";

        /// <summary>
        /// Serializes all custom data fields into the byte array.
        /// </summary>
        /// <returns>Byte array representing custom data.</returns>
        internal byte[] Serialize()
        {
            using (MemoryStream m = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(m))
                {
                    writer.Write(OriginalPath);
                }
                return m.ToArray();
            }
        }

        /// <summary>
        /// Deserializes custom data from byte array into object.
        /// </summary>
        /// <param name="data">Byte array representing custom data.</param>
        /// <returns></returns>
        internal static CustomData Desserialize(byte[] data)
        {
            if(data == null)
            {
                throw new ArgumentNullException("data");
            }

            CustomData obj = new CustomData();
            using (MemoryStream m = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(m))
                {
                    obj.OriginalPath = reader.ReadString();
                }
            }
            return obj;
        }
    }

    /// <summary>
    /// Placeholder methods to get and set custom data associated with a placeholder, such as OriginalPath.
    /// </summary>
    internal static class PlaceholderItemExtensions
    {
        public static void SetCustomData(this PlaceholderItem placeholder, string originalPath)
        {
            CustomData customData = new CustomData { OriginalPath = originalPath };
            placeholder.SetCustomData(customData.Serialize());
        }

        public static void SetCustomData(Microsoft.Win32.SafeHandles.SafeFileHandle safeHandle, string originalPath)
        {
            CustomData customData = new CustomData { OriginalPath = originalPath };
            PlaceholderItem.SetCustomData(safeHandle, customData.Serialize());
        }

        public static void SetOriginalPath(this PlaceholderItem placeholder, string originalPath)
        {
            byte[] customDataRaw = placeholder.GetCustomData();
            CustomData customData = (customDataRaw.Length > 0) ? CustomData.Desserialize(customDataRaw) : new CustomData();

            customData.OriginalPath = originalPath;
            placeholder.SetCustomData(customData.Serialize());
        }

        public static string GetOriginalPath(this PlaceholderItem placeholder)
        {
            byte[] customDataRaw = placeholder.GetCustomData();
            CustomData customData = (customDataRaw.Length > 0) ? CustomData.Desserialize(customDataRaw) : new CustomData();
            return customData.OriginalPath;
        }

        /// <summary>
        /// Returns true if the file was moved in the user file system and 
        /// changes not yet synched to the remote storage.
        /// </summary>
        public static bool IsMoved(this PlaceholderItem placeholder)
        {
            // If original path was never set, the file was just created, not moved.
            string originalPath = placeholder.GetOriginalPath();
            if (string.IsNullOrEmpty(originalPath))
            {
                return false;
            }

            // Otherwise verify that current file path and original file path are equal.
            return !originalPath.TrimEnd(Path.DirectorySeparatorChar).Equals(placeholder.Path.TrimEnd(Path.DirectorySeparatorChar), StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Returns true if the item was created and must be synched to remote storage.
        /// </summary>
        /// <returns>
        /// True if the item was created in the user file system and does not exists 
        /// in the remote storage. False otherwise.
        /// </returns>
        public static bool IsNew(this PlaceholderItem placeholder)
        {
            // ETag absence signals that the item is new.
            // However, ETag file may not exists during move operation, 
            // additionally checking OriginalPath presence.
            // Can not rely on OriginalPath only, 
            // because MS Office files are being deleted and re-created during transactional save.

            string originalPath = placeholder.GetOriginalPath();

            bool eTagFileExists = File.Exists(ETag.GetETagFilePath(placeholder.Path));

            return !eTagFileExists && string.IsNullOrEmpty(originalPath);
        }

    }
}
