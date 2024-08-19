using System;
using ITHit.FileSystem;

namespace WebDAVCommon
{
	public class BookmarkUtils
	{
        private readonly ILogger Logger;

        public BookmarkUtils(ILogger logger)
        {
            Logger = logger;
        }

        public string GarantAccessDialog(NSUrl directoryUrl)
        {
            NSOpenPanel openPanel = new NSOpenPanel
            {
                CanChooseFiles = false,
                CanChooseDirectories = true,
                AllowsMultipleSelection = false,
                ResolvesAliases = true,
                Title = "Select a Folder",
                Prompt = "Garant Access",
                DirectoryUrl = directoryUrl
            };

            if (openPanel.RunModal() == 1)
            {
                NSUrl folderUrl = openPanel.Urls[0];
                return folderUrl.Path;
            }

            return string.Empty;
        }

        public void SaveBookmarkForFolder(string folderPath, string bookmarkPath)
        {
            NSUrl folderUrl = NSUrl.FromString($"file://{folderPath}");

            NSError error;
            NSData bookmarkData = folderUrl.CreateBookmarkData(NSUrlBookmarkCreationOptions.WithSecurityScope, null, null, out error);

            if (error != null)
            {
                Logger.LogError($"Error creating bookmark: {error.LocalizedDescription}");
            }
            else
            {
                File.WriteAllBytes(bookmarkPath, bookmarkData.ToArray());
                Logger.LogDebug($"Bookmark {bookmarkPath} saved.");
            }
        }

        public bool OpenFolderFromBookmark(string bookmarkPath, string itemPath)
        {
            if (!File.Exists(bookmarkPath)) return false;

            try
            {
                NSData bookmarkData = NSData.FromArray(File.ReadAllBytes(bookmarkPath));

                bool bookmarkIsStale;
                NSError error;
                NSUrl folderUrl = NSUrl.FromBookmarkData(bookmarkData, NSUrlBookmarkResolutionOptions.WithSecurityScope, null, out bookmarkIsStale, out error);

                if (error != null || bookmarkIsStale)
                {
                    Logger.LogError($"Error resolving bookmark: {error?.LocalizedDescription}");
                    return false;
                }

                bool startAccessing = folderUrl.StartAccessingSecurityScopedResource();
                if (startAccessing)
                {
                    Logger.LogDebug($"Access restored to folder: {folderUrl.Path}");

                    Logger.LogDebug($"Item path: {NSUrl.FromString($"file://{itemPath}")}");

                    // Open file/folder.
                    NSWorkspace.SharedWorkspace.OpenUrl(NSUrl.FromString($"file://{itemPath}"));

                    folderUrl.StopAccessingSecurityScopedResource();
                    return true;
                }
                else
                {
                    Logger.LogError("Failed to start accessing security scoped resource.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception restoring access: {ex.Message}");
                return false;
            }
        }
    }
}

