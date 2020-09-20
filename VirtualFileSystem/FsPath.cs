using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace VirtualFileSystem
{
    /// <summary>
    /// Provides file system operations. Helps determining file and folder existence and creating file and folder items.
    /// </summary>
    public static class FsPath
    {
        /// <summary>
        /// Returns true if the path points to a file. False - otherwise.
        /// </summary>
        /// <remarks>Throws exception if the file/folder does not exists.</remarks>
        /// <param name="path">Path to the file or folder.</param>
        /// <returns>True if the path is file. False - otherwise.</returns>
        public static bool IsFile(string path)
        {
            return !IsFolder(path);
        }

        /// <summary>
        /// Returns true if the path points to a folder. False - otherwise.
        /// </summary>
        /// <remarks>Throws exception if the file/folder does not exists.</remarks>
        /// <param name="path">Path to the file or folder.</param>
        /// <returns>True if the path is folder. False - otherwise.</returns>
        public static bool IsFolder(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.Directory) == FileAttributes.Directory;
        }

        /// <summary>
        /// Returns true if a file or folder exists under the specified path. False - otherwise.
        /// </summary>
        /// <remarks>Does not throw exceptions.</remarks>
        /// <param name="path">Path to the file or folder.</param>
        /// <returns>True if the file or folder exists under specified path. False - otherwise.</returns>
        public static bool Exists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        /// <summary>
        /// Returns true if the path point to a recycle bin folder.
        /// </summary>
        /// <param name="path">Path to the file or folder.</param>
        public static bool IsRecycleBin(string path)
        {
            return path.IndexOf("\\$Recycle.Bin", StringComparison.InvariantCultureIgnoreCase) != -1;
        }

        /// <summary>
        /// Returns storage item or null if item does not eists.
        /// </summary>
        /// <param name="path">Path to the file or folder.</param>
        /// <returns>
        /// Instance of <see cref="Windows.Storage.StorageFile"/> or <see cref="Windows.Storage.StorageFolder"/> 
        /// that corresponds to path or null if item does not exists.
        /// </returns>
        public static async Task<Windows.Storage.IStorageItem> GetStorageItemAsync(string path)
        {
            if (File.Exists(path))
            {
                return await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
            }
            if (Directory.Exists(path))
            {
                return await Windows.Storage.StorageFolder.GetFolderFromPathAsync(path);
            }
            return null;
        }

        /// <summary>
        /// Returns folder or file item or null if the item does not exists.
        /// </summary>
        /// <param name="path">Path to the file or folder.</param>
        /// <returns>
        /// Instance of <see cref="FileInfo"/> or <see cref="DirectoryInfo"/> 
        /// that corresponds to path or null if item does not exists.
        /// </returns>
        public static FileSystemInfo GetFileSystemItem(string path)
        {
            if (File.Exists(path))
            {
                return new FileInfo(path);
            }
            if (Directory.Exists(path))
            {
                return new DirectoryInfo(path);
            }
            return null;
        }

        /// <summary>
        /// Gets file or folder attributes in a human-readable form.
        /// </summary>
        /// <param name="path">Path to the file or folder.</param>
        /// <returns>String that represents file or folder attributes or null if the file/folder is not found.</returns>
        public static string GetAttString(string path)
        {
            try
            {
                return File.GetAttributes(path).ToString();
            }
            catch 
            {
                return null;
            }
        }

        /// <summary>
        /// Returns true if the file hase a format ~XXXXX.tmp, false - otherwise.
        /// </summary>
        /// <param name="path">Path to a file or folder.</param>
        public static bool IsMsOfficeTemp(string path)
        {
            return Path.GetFileName(path).StartsWith('~') && Path.GetExtension(path).Equals(".tmp", StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool IsMsOfficeLocked(string path)
        {
            //string lockFileName = $"~${Path.GetFileName(path)}";
            //string lockPath = Path.Combine(Path.GetDirectoryName(path), lockFileName);

            int separatorIndex = path.LastIndexOf(Path.DirectorySeparatorChar);
            string lockPath = path.Insert(separatorIndex + 1, "~$");
            return File.Exists(lockPath);
        }

        /// <summary>
        /// Returns true if the file or folder is marked with Hidden or Temporaty attributes.
        /// </summary>
        /// <param name="path">Path to a file or folder.</param>
        /// <returns>
        /// True if the file or folder is marked with Hidden or Temporaty attributes. 
        /// Returns false if no Hidden or Temporaty attributes found or file/folder does not exists.
        /// </returns>
        public static bool IsHiddenOrTemp(string path)
        {
            if(!FsPath.Exists(path))
            {
                return false;
            }

            FileAttributes att = File.GetAttributes(path);
            return ( (att & System.IO.FileAttributes.Hidden) != 0)
                 || ((att & System.IO.FileAttributes.Temporary) != 0);
        }

        /// <summary>
        /// Returns true if the file or folder should not be syched between the user file 
        /// system and the remote storage. False - otherwise.
        /// </summary>
        /// <param name="path">Path to a file or folder.</param>
        public static bool AvoidSync(string path)
        {
            return IsMsOfficeLocked(path) || IsMsOfficeTemp(path) || IsHiddenOrTemp(path);
        }

        /// <summary>
        /// Gets formatted file size or null for folders or if the file is not found.
        /// </summary>
        /// <param name="path">Path to a file or folder.</param>
        public static string Size(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            long length;
            try
            {
                length = new FileInfo(path).Length;
            }
            catch
            {
                return null;
            }

            string[] suf = { "b ", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (length == 0)
            {
                return "0" + suf[0];
            }
            long bytes = Math.Abs(length);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(length) * num).ToString() + suf[place];
        }
    }
}
