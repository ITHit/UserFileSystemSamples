using System;
using System.Collections.Generic;
using System.Text;
using System.IO;


namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Provides methods to detect Microsoft Office files that should not be synced to the remote storage, 
    /// such as temporary files, Microsoft Office lock files and MS Office files opened (locked) for editing.
    /// </summary>
    internal class MsOfficeFilterHelper
    {
        /// <summary>
        /// Returns true if the file should NOT be synched to the remote storage. False - otherwise.
        /// </summary>
        /// <param name="path">Path to a file or folder.</param>
        internal static bool AvoidSync(string path)
        {
            return IsMsOfficeLockFile(path) || IsMsOfficeTemp(path) || FilterHelper.IsHiddenOrTemp(path);
        }

        /// <summary>
        /// Returns true if file system contains MS Office lock-file (~$file.ext) in file 
        /// system that corresponds to the provided path to MS Office file. False - otherwise.
        /// </summary>
        /// <param name="path">Path to the MS Office file.</param>
        internal static bool IsMsOfficeLocked(string path)
        {
            string lockPath = GetLockPathFromMsOfficePath(path);
            return lockPath != null;
        }

        /// <summary>
        /// Returns true if the file is in ~XXXXX.tmp, pptXXXX.tmp format, false - otherwise.
        /// </summary>
        /// <param name="path">Path to a file.</param>
        private static bool IsMsOfficeTemp(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            return (fileName.StartsWith('~') && extension.Equals(".tmp", StringComparison.InvariantCultureIgnoreCase))    // Word temp files
                || (fileName.StartsWith("ppt") && extension.Equals(".tmp", StringComparison.InvariantCultureIgnoreCase))  // PowerPoint temp files
                || (string.IsNullOrEmpty(extension) && (fileName.Length == 8) && System.IO.File.Exists(path))             // Excel temp files type 1
                || (((fileName.Length == 8) || (fileName.Length == 7)) && extension.Equals(".tmp", StringComparison.InvariantCultureIgnoreCase));   // Excel temp files type 2
        }

        /// <summary>
        /// Returns true if the provided path points to MS Office lock file (~$file.ext). 
        /// </summary>
        /// <param name="path">Path to the MS Office lock file.</param>
        private static bool IsMsOfficeLockFile(string path)
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
    }
}
