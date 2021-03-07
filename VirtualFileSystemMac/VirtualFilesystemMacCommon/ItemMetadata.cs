using System;
using Foundation;

namespace VirtualFilesystemMacCommon
{
    public class ItemMetadata
    {
        public enum ItemMetadataType
        {
            Unknown,
            Dir,
            File
        }

        public ItemMetadata(string identifier)
        {
            Identifier = identifier;
            ItemType = ItemMetadataType.Unknown;
        }

        public ItemMetadata(string identifier, string parentIdentifier, NSDate creationDate, NSDate modificationDate, ItemMetadataType itemType = ItemMetadataType.Unknown, ulong fileSize = 0)
        {
            Identifier = identifier;
            ParentIdentifier = parentIdentifier;
            CreationDate = creationDate;
            ModificationDate = modificationDate;
            ItemType = itemType;
            FileSize = new NSNumber(fileSize);
        }

        public string Identifier { get; set; }
        public string ParentIdentifier { get; set; }
        public NSDate CreationDate { get; set; }
        public NSDate ModificationDate { get; set; }
        public NSNumber FileSize { get; set; }
        public ItemMetadataType ItemType { get; set; }
    }
}
