using System;
using System.Collections.Generic;
using System.Text;

namespace VirtualDrive.ShellExtension.Interop
{
    public static class ShellItemHelper
    {
        public static IEnumerable<string> GetFilesPath(this IShellItemArray shellItems)
        {
            if (shellItems.GetCount(out ushort shellItemCount) != WinError.S_OK)
                throw new ArgumentException();

            List<string> files = new List<string>();

            for (ushort i = 0; i < shellItemCount; i++)
            {
                if (shellItems.GetItemAt(i, out IShellItem shellItem) != WinError.S_OK)
                    continue;

                if (shellItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string path) != WinError.S_OK)
                    continue;

                files.Add(path);
            }

            return files;
        }

        public static string GetFilePath(this IShellItem shellItem)
        {
            if (shellItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string path) != WinError.S_OK)
                throw new ArgumentException();

            return path;
        }
    }
}
