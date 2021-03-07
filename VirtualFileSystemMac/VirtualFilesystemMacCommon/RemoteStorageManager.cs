using ITHit.FileSystem;
using Foundation;
using System.Collections.Generic;
using System.IO;

namespace VirtualFilesystemMacCommon
{
    public class RemoteStorageManager
    {
        private FsEngine RemoteEngine;
        private LocationMapper RemoteLocationMapper;
        private ConsoleLogger Logger;

        public RemoteStorageManager(string remotePath)
        {
            RemoteEngine = new FsEngine(remotePath);
            RemoteLocationMapper = new LocationMapper(remotePath);
            Logger = new ConsoleLogger(GetType().Name);
        }

        public List<ItemMetadata> GetFolderContents(string identifier)
        {
            List<ItemMetadata> items = new List<ItemMetadata>();
            string remotePath = RemoteLocationMapper.GetRemotePathFromIdentifier(identifier);

            IFileSystemItem item = RemoteEngine.GetFileSystemItem(remotePath);
            if (item is null || !item.GetType().Equals(typeof(UserFolder)))
            {
                return items;
            }

            Logger.LogDebug("[TEST] GetFolderContents remote root path: " + RemoteLocationMapper.GetParentForIdentifier(remotePath) + ". from remote root: " + remotePath);

            UserFolder folder = (UserFolder)item;
            FileSystemItemBasicInfo[] folderChildren = folder.GetChildren("*");
            for (long childIndex = 0; childIndex < folderChildren.Length; ++childIndex)
            {
                FileSystemItemBasicInfo child = folderChildren[childIndex];
                items.Add(new ItemMetadata(RemoteLocationMapper.GetIdentifierFromRemotePath(child.Name), RemoteLocationMapper.GetParentForIdentifier(child.Name), (NSDate)child.CreationTime,
                                            (NSDate)child.ChangeTime, GetPathType(child), child.Size));
            }

            return items;
        }

        public ItemMetadata GetItem(string identifier)
        {
            string remotePath = RemoteLocationMapper.GetRemotePathFromIdentifier(identifier);
            ItemMetadata emptyItem = new ItemMetadata(remotePath);

            IFileSystemItem item = RemoteEngine.GetFileSystemItem(remotePath);
            if (item is null)
            {
                Logger.LogError("Filesystem error getting item at path: " + remotePath);
                return emptyItem;
            }

            Logger.LogDebug("[TEST] GetItem remote root path: " + RemoteLocationMapper.GetParentForIdentifier(remotePath) + ". from remote root: " + remotePath);

            if (item.GetType().Equals(typeof(UserFolder)))
            {
                UserFolder folder = (UserFolder)item;
                return new ItemMetadata(RemoteLocationMapper.GetIdentifierFromRemotePath(remotePath), RemoteLocationMapper.GetParentForIdentifier(remotePath), (NSDate)folder.FolderInfo.CreationTime,
                                        (NSDate)folder.FolderInfo.ChangeTime, GetPathType(folder.FolderInfo), folder.FolderInfo.Size);
            }
            else if (item.GetType().Equals(typeof(UserFile)))
            {
                UserFile file = (UserFile)item;
                return new ItemMetadata(RemoteLocationMapper.GetIdentifierFromRemotePath(remotePath), RemoteLocationMapper.GetParentForIdentifier(remotePath), (NSDate)file.FileInfo.CreationTime,
                                        (NSDate)file.FileInfo.ChangeTime, GetPathType(file.FileInfo), file.FileInfo.Size);
            }

            return emptyItem;
        }

        private ItemMetadata.ItemMetadataType GetPathType(IFileSystemItemBasicInfo fileInfo)
        {
            return (fileInfo.Attributes.HasFlag(FileAttributes.Directory) ? ItemMetadata.ItemMetadataType.Dir : ItemMetadata.ItemMetadataType.File);
        }
    }
}
