using System;
using System.Collections.Generic;
using System.Text;
using System.IO;


namespace WebDAVDrive
{
    /// <summary>
    /// Microsoft Office helper methods.
    /// </summary>
    internal class MsOfficeHelper
    {
        /// <summary>
        /// Returns true if the path points to a recycle bin folder.
        /// </summary>
        /// <param name="path">Path to the file or folder.</param>
        public static bool IsRecycleBin(string path)
        {
            return path.IndexOf("\\$Recycle.Bin", StringComparison.InvariantCultureIgnoreCase) != -1;
        }

        /// <summary>
        /// Returns true if the file is in ~XXXXX.tmp, pptXXXX.tmp format, false - otherwise.
        /// </summary>
        /// <param name="path">Path to a file.</param>
        private static bool IsMsOfficeTemp(string path)
        {
            return (Path.GetFileName(path).StartsWith('~') && Path.GetExtension(path).Equals(".tmp", StringComparison.InvariantCultureIgnoreCase))    // Word temp files
                || (Path.GetFileName(path).StartsWith("ppt") && Path.GetExtension(path).Equals(".tmp", StringComparison.InvariantCultureIgnoreCase))    // PowerPoint temp files
                || (string.IsNullOrEmpty(Path.GetExtension(path)) && (Path.GetFileName(path).Length == 8) && System.IO.File.Exists(path))                         // Excel temp files
                || (((Path.GetFileNameWithoutExtension(path).Length == 8) || (Path.GetFileNameWithoutExtension(path).Length == 7)) && Path.GetExtension(path).Equals(".tmp", StringComparison.InvariantCultureIgnoreCase));   // Excel temp files
        }

        /// <summary>
        /// Returns true if file system contains MS Office lock file (~$file.ext) in file 
        /// system that corresponds to the provided path to MS Office file. 
        /// </summary>
        /// <param name="path">Path to the MS Office file.</param>
        public static bool IsMsOfficeLocked(string path)
        {
            string lockPath = GetLockPathFromMsOfficePath(path);
            return lockPath != null;
        }

        /// <summary>
        /// Returns true if the provided path points to MS Office lock file (~$file.ext). 
        /// </summary>
        /// <param name="path">Path to the MS Office lock file.</param>
        internal static bool IsMsOfficeLockFile(string path)
        {
            return Path.GetFileName(path).StartsWith("~$");
        }

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
                if (System.IO.File.Exists(msOfficeLockFilePath))
                {
                    return msOfficeLockFilePath;
                }
                int fileNameLength = Path.GetFileNameWithoutExtension(msOfficeFilePath).Length;
                if (fileNameLength > 6)
                {
                    int removeChars = fileNameLength == 7 ? 1 : 2;
                    msOfficeLockFilePath = msOfficeLockFilePath.Remove(separatorIndex + 1 + "~$".Length, removeChars);
                    if (System.IO.File.Exists(msOfficeLockFilePath))
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
            if (!System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
            {
                return false;
            }

            FileAttributes att = System.IO.File.GetAttributes(path);
            return ((att & System.IO.FileAttributes.Hidden) != 0)
                 || ((att & System.IO.FileAttributes.Temporary) != 0);
        }

        /// <summary>
        /// Returns true if the file or folder should not be automatically locked. False - otherwise.
        /// </summary>
        /// <param name="path">Path to a file or folder.</param>
        public static bool AvoidMsOfficeSync(string path)
        {
            return IsMsOfficeLockFile(path) || IsMsOfficeTemp(path) || IsHiddenOrTemp(path);
        }
    }
}
