using System;
using System.IO;
using ITHit.FileSystem;

namespace VirtualFilesystemMacCommon
{
    public class FileSystemItemBasicInfo : IFileSystemItemBasicInfo
    {
        public FileSystemItemBasicInfo(string name, FileAttributes attributes, DateTime creationTime, DateTime changeTime, ulong size = 0)
        {
            Name = name;
            Attributes = attributes;
            CreationTime = creationTime;
            ChangeTime = changeTime;
            Size = size;
        }

        public string Name { get; }

        public FileAttributes Attributes { get; }

        public byte[] CustomData { get; }

        public DateTime CreationTime { get; }

        public DateTime LastWriteTime { get; }

        public DateTime LastAccessTime { get; }

        public DateTime ChangeTime { get; }

        public ulong Size { get; }
    }
}
