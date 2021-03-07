using System.IO;
using FileProvider;
using UniformTypeIdentifiers;

namespace VirtualFilesystemMacCommon
{
    public class LocationMapper
    {
        public string RemoteRootPath { get; }

        public LocationMapper(string remoteRootPath)
        {
            RemoteRootPath = remoteRootPath;
        }

        public string GetIdentifierFromRemotePath(string path)
        {
            if (path == RemoteRootPath)
            {
                return NSFileProviderItemIdentifier.RootContainer;
            }

            return path;
        }

        public string GetPathFromRemotePath(string path)
        {
            if (path == RemoteRootPath)
            {
                return "/";
            }

            return path;
        }

        public string GetRemotePathFromIdentifier(string identifier)
        {
            if (identifier == NSFileProviderItemIdentifier.RootContainer)
            {
                return RemoteRootPath;
            }

            if (identifier == NSFileProviderItemIdentifier.WorkingSetContainer)
            {
                return NSFileProviderItemIdentifier.WorkingSetContainer;
            }

            if (identifier == NSFileProviderItemIdentifier.TrashContainer)
            {
                return NSFileProviderItemIdentifier.TrashContainer;
            }

            return identifier;
        }

        public string GetParentForIdentifier(string identifier)
        {
            DirectoryInfo parentInfo = Directory.GetParent(identifier);
            if (parentInfo == null || parentInfo.FullName == RemoteRootPath || identifier == RemoteRootPath)
            {
                return NSFileProviderItemIdentifier.RootContainer;
            }

            return parentInfo.FullName;
        }

        public static string GetFileNameFromIdentifier(string identifier)
        {
            if (identifier == NSFileProviderItemIdentifier.RootContainer || identifier == NSFileProviderItemIdentifier.WorkingSetContainer || identifier == NSFileProviderItemIdentifier.TrashContainer)
            {
                return "/";
            }

            return Path.GetFileName(identifier);
        }

        public static bool IsRootItem(string identifier)
        {
            return (identifier == NSFileProviderItemIdentifier.RootContainer);
        }

        public static bool IsWorkingSetOrTrashItem(string identifier)
        {
            return identifier == NSFileProviderItemIdentifier.WorkingSetContainer || identifier == NSFileProviderItemIdentifier.TrashContainer;
        }
    }
}
