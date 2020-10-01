using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VirtualFileSystem
{
    /// <summary>
    /// Custom data stored with a file or folder placeholder, such ETag and original file/folder path. Max 4KB.
    /// </summary>
    /// <remarks>To avoid storing metatadata and keep footprit small, this class is is using custom serialization.</remarks>
    internal class CustomData
    {
        /// <summary>
        /// File ETag. Used to verify that the file on the server is not modified during client to server synchronization.
        /// </summary>
        /// <remarks>This field is required if the server does not provide locking capabilities.</remarks>
        internal string ETag = "";

        /// <summary>
        /// Keeps the original file/folder path. Used to sync file/folder from user file system to remote storage 
        /// if this app was not running when the file/folder was moved or renamed. This field allows to avoid 
        /// delete-create sequence during client to server synchronization after app failure.
        /// </summary>
        internal string OriginalPath = "";

        /// <summary>
        /// Used for Microsoft Office lock files (~$file.ext) to store custom data during transactional save.
        /// The original MS Office file is renamed and than deleted. As a result the ETag is lost and we can not 
        /// send the ETag to the server when saving the file. 
        /// As a solution, we copy custom data from the original file during lock file creation into this field. 
        /// When the original file is being saved, we read ETag from the lock file.
        /// </summary>
        internal byte[] SavedData = new byte[] { };

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
                    writer.Write(ETag);
                    writer.Write(OriginalPath);
                    writer.Write(SavedData.Length);
                    writer.Write(SavedData);
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
                    obj.ETag = reader.ReadString();
                    obj.OriginalPath = reader.ReadString();
                    obj.SavedData = reader.ReadBytes(reader.ReadInt32());
                }
            }
            return obj;
        }
    }

    /// <summary>
    /// Placeholder methods to get and set custom data associated with a placeholder, such as ETah and OriginalPath.
    /// </summary>
    internal static class PlaceholderItemExtensions
    {
        public static void SetCustomData(this PlaceholderItem placeholder, string eTag, string originalPath)
        {
            CustomData customData = new CustomData { ETag = eTag, OriginalPath = originalPath };
            placeholder.SetCustomData(customData.Serialize());
        }

        public static void SetCustomData(Microsoft.Win32.SafeHandles.SafeFileHandle safeHandle, string eTag, string originalPath)
        {
            CustomData customData = new CustomData { ETag = eTag, OriginalPath = originalPath };
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

        public static void SetETag(this PlaceholderItem placeholder, string eTag)
        {
            byte[] customDataRaw = placeholder.GetCustomData();
            CustomData customData = (customDataRaw.Length > 0) ? CustomData.Desserialize(customDataRaw) : new CustomData();

            customData.ETag = eTag;
            placeholder.SetCustomData(customData.Serialize());
        }

        public static string GetETag(this PlaceholderItem placeholder)
        {
            byte[] customDataRaw = placeholder.GetCustomData();
            CustomData customData = (customDataRaw.Length > 0) ? CustomData.Desserialize(customDataRaw) : new CustomData();
            return customData.ETag;
        }

        public static void SetSavedData(this PlaceholderItem placeholder, byte[] saveData)
        {
            byte[] customDataRaw = placeholder.GetCustomData();
            CustomData customData = (customDataRaw.Length > 0) ? CustomData.Desserialize(customDataRaw) : new CustomData();

            customData.SavedData = saveData;
            placeholder.SetCustomData(customData.Serialize());
        }

        public static byte[] GetSavedData(this PlaceholderItem placeholder)
        {
            byte[] customDataRaw = placeholder.GetCustomData();
            CustomData customData = (customDataRaw.Length > 0) ? CustomData.Desserialize(customDataRaw) : new CustomData();
            return customData.SavedData;
        }

        /// <summary>
        /// Returns true if the file was moved in the user file system and changes not yet synched to the remote storage.
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
            return !originalPath.Equals(placeholder.Path, StringComparison.InvariantCultureIgnoreCase);
        }

    }
}
