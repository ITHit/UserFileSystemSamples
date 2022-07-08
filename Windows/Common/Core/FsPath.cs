using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using FileAttributes = System.IO.FileAttributes;
using ITHit.FileSystem.Windows;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Provides file system operations. Helps determining file and folder existence and creating file and folder items.
    /// </summary>
    public static class FsPath
    {
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
    }
}
