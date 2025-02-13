using ITHit.FileSystem;
using System;

namespace WebDAVDrive.Extensions
{
    /// <summary>
    /// Provides extension methods for user friendly presentations of OperationType enum values
    /// </summary>
    public static class OperationTypeExtension
    {
        /// <summary>
        /// Shows user-friendly operation name, like Deletion, Creation, etc.
        /// </summary>
        /// <param name="type">OperationType value from engine.</param>
        /// <param name="direction">Sync direction.</param>
        public static string GetFriendlyName(this OperationType type, SyncDirection direction)
        {
            if (type == OperationType.Populate)
            {
                return "Folder listing";
            }
            else if (type == OperationType.UpdateContent)
            {
                return direction == SyncDirection.Incoming ? "Download" : "Upload";
            }
            else if (type == OperationType.UpdateMetadata)
            {
                return "Metadata update";
            }
            else if (type == OperationType.DeleteCompletion)
            {
                return "Deleted";
            }
            else if (type == OperationType.Create)
            {
                return "Creation";
            }
            else if (type == OperationType.CreateCompletion)
            {
                return "Created";
            }
            else if (type == OperationType.MoveCompletion)
            {
                return "Moved";
            }
            else if (type == OperationType.ShadowDownload)
            {
                return "Shadow downloading";
            }
            //other names are user-friendly by themselves, like Lock, Unlock, etc.
            else
            {
                return Enum.GetName(type)!;
            }
        }
    }
}
