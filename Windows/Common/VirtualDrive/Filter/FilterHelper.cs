using System;
using System.Collections.Generic;
using System.Text;
using System.IO;


namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Provides methods to detect hidden files, temporary files, lock files and files moved to recycle bin.
    /// </summary>
    public class FilterHelper
    {
        /// <summary>
        /// Returns true if the file should NOT be synched to the remote storage. False - otherwise.
        /// </summary>
        /// <param name="path">Path to a file or folder.</param>
        public static bool AvoidSync(string path)
        {
            return MsOfficeFilterHelper.AvoidSync(path) || AutoCadFilterHelper.AvoidSync(path) || IsHiddenOrTemp(path);
        }

        /// <summary>
        /// Returns true if file system contains an application lock-file (~$*.docx, *.dwl, *.dwl2, etc) in the file 
        /// system that corresponds to the provided path to the file file. False - otherwise.
        /// </summary>
        /// <param name="path">Path to the file to check the lock-file for.</param>
        public static bool IsAppLocked(string path)
        {
            return MsOfficeFilterHelper.IsMsOfficeLocked(path) || AutoCadFilterHelper.IsAutoCadLocked(path);
        }


        /// <summary>
        /// Returns true if the path points to a recycle bin folder.
        /// </summary>
        /// <param name="path">Path to the file or folder.</param>
        public static bool IsRecycleBin(string path)
        {
            return path.IndexOf("\\$Recycle.Bin", StringComparison.InvariantCultureIgnoreCase) != -1;
        }

        /// <summary>
        /// Returns true if the file or folder is marked with Hidden or Temporaty attributes.
        /// </summary>
        /// <param name="path">Path to a file or folder.</param>
        /// <returns>
        /// True if the file or folder is marked with Hidden or Temporaty attributes. 
        /// Returns false if no Hidden or Temporaty attributes found or file/folder does not exists.
        /// </returns>
        internal static bool IsHiddenOrTemp(string path)
        {
            if (!System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
            {
                return false;
            }

            FileAttributes att = System.IO.File.GetAttributes(path);
            return ((att & System.IO.FileAttributes.Hidden) != 0)
                 || ((att & System.IO.FileAttributes.Temporary) != 0);
        }
    }
}
