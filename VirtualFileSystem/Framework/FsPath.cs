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
        private static bool IsMsOfficeTemp(string path)
        {
            return (Path.GetFileName(path).StartsWith('~')   && Path.GetExtension(path).Equals(".tmp", StringComparison.InvariantCultureIgnoreCase))  // Word temp files
                || (Path.GetFileName(path).StartsWith("ppt") && Path.GetExtension(path).Equals(".tmp", StringComparison.InvariantCultureIgnoreCase)) // PowerPoint temp files
                || (string.IsNullOrEmpty(Path.GetExtension(path)) && (Path.GetFileName(path).Length == 8)); // Excel temp files
        }

        /// <summary>
        /// Returns true if file system contains MS Office lock file (~$file.ext) in file 
        /// system that corresponds to the provided path to MS Office file. 
        /// </summary>
        /// <param name="path">Path to MS Office file.</param>
        private static bool IsMsOfficeLocked(string path)
        {
            string lockPath = GetLockPathFromMsOfficePath(path);
            return lockPath != null;
        }


        /// <summary>
        /// Returns true if the provided path points to MS Office lock file (~$file.ext). 
        /// </summary>
        /// <param name="path">Path to lock file.</param>
        private static bool IsMsOfficeLockFile(string path)
        {
            return Path.GetFileName(path).StartsWith("~$");
        }

        /*
        public static string GetMsOfficePathFromLock(string msOfficeLockFilePath)
        {
            int separatorIndex = msOfficeLockFilePath.LastIndexOf(Path.DirectorySeparatorChar);
            return msOfficeLockFilePath.Remove(separatorIndex + 1, "~$".Length);
        }
        */

        /// <summary>
        /// Returns MS Office lock file path if such file exists.
        /// </summary>
        /// <param name="msOfficeFilePath">MS Office file path.</param>
        /// <returns>Lock file path.</returns>
        /// <remarks>
        /// mydoc.docx       -> ~$mydoc.docx
        /// mydocfi.docx     -> ~$ydocfi.docx
        /// mydocfile.docx   -> ~$docfile.docx
        /// mydocfile.pptx   -> ~$mydocfile.pptx
        /// mydocfile.ppt    -> ~$mydocfile.ppt
        /// mydocfile.xlsx   -> ~$mydocfile.xlsx
        /// mydocfile.xls    -> null
        /// </remarks>
        private static string GetLockPathFromMsOfficePath(string msOfficeFilePath)
        {
            string msOfficeLockFilePath = null;
            int separatorIndex = msOfficeFilePath.LastIndexOf(Path.DirectorySeparatorChar);
            if ((separatorIndex != -1) && !string.IsNullOrEmpty(Path.GetExtension(msOfficeFilePath)))
            {
                msOfficeLockFilePath = msOfficeFilePath.Insert(separatorIndex + 1, "~$");
                if(FsPath.Exists(msOfficeLockFilePath))
                {
                    return msOfficeLockFilePath;
                }
                int fileNameLength = Path.GetFileNameWithoutExtension(msOfficeFilePath).Length;
                if (fileNameLength > 6)
                {
                    int removeChars = fileNameLength == 7 ? 1 : 2;
                    msOfficeLockFilePath = msOfficeLockFilePath.Remove(separatorIndex + 1 + "~$".Length, removeChars);
                    if (FsPath.Exists(msOfficeLockFilePath))
                    {
                        return msOfficeLockFilePath;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Returns true if the file or folder is marked with Hidden or Temporaty attributes.
        /// </summary>
        /// <param name="path">Path to a file or folder.</param>
        /// <returns>
        /// True if the file or folder is marked with Hidden or Temporaty attributes. 
        /// Returns false if no Hidden or Temporaty attributes found or file/folder does not exists.
        /// </returns>
        private static bool IsHiddenOrTemp(string path)
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
            return IsMsOfficeLockFile(path) || IsMsOfficeLocked(path) || IsMsOfficeTemp(path) || IsHiddenOrTemp(path);
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
