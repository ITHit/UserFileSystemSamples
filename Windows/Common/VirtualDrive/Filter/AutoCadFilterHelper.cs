using System;
using System.Collections.Generic;
using System.Text;
using System.IO;


namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Provides methods to detect AutoCAD files that should not be synced to the remote storage, 
    /// such as AutoCAD temporary files and AutoCAD lock files.
    /// </summary>
    /// <remarks>
    /// AutoCAD lock files (.dwl and .dwl2), are marked as hidden and filtered by the <see cref="FilterHelper.IsHiddenOrTemp"/> method.
    /// </remarks>
    public class AutoCadFilterHelper
    {
        /// <summary>
        /// Returns true if the file should NOT be synched to the remote storage. False - otherwise.
        /// </summary>
        /// <param name="path">Path to a file or folder.</param>
        public static bool AvoidSync(string path)
        {
            return IsAutoCadTemp(path) || FilterHelper.IsHiddenOrTemp(path);
        }

        /// <summary>
        /// Returns true if the file has .tmp extension, false - otherwise.
        /// </summary>
        /// <param name="path">Path to a file.</param>
        /// <remarks>
        /// sav1ea4afdc.tmp
        /// Mechanical - Multileaders8f31c74f.tmp
        /// Mechanical - Multileaders.bak
        /// atmp91130947
        /// </remarks>
        private static bool IsAutoCadTemp(string path)
        {
            string extension = Path.GetExtension(path);
            return (extension.Equals(".tmp", StringComparison.InvariantCultureIgnoreCase)
                || extension.Equals(".bak", StringComparison.InvariantCultureIgnoreCase)
                || (string.IsNullOrEmpty(extension) && Path.GetFileName(path).StartsWith("atmp", StringComparison.InvariantCultureIgnoreCase))
                );
        }

        /// <summary>
        /// Returns true if file system contains an AutoCAD lock-file (.dwl or .dwl2) in file 
        /// system that corresponds to the provided path to AutoCAD file. False - otherwise.
        /// </summary>
        /// <param name="path">Path to the AutoCAD file.</param>
        public static bool IsAutoCadLocked(string path)
        {
            string lockPath = GetLockPathFromAutoCadPath(path);
            return lockPath != null;
        }

        /// <summary>
        /// Returns AutoCAD lock file path if such file exists. Null - otherwise.
        /// </summary>
        /// <param name="msOfficeFilePath">AutoCAD file path.</param>
        /// <returns>Lock file path or null if the lock file is not found.</returns>
        /// <remarks>
        /// file.dwg -> file.dwl
        /// </remarks>
        private static string GetLockPathFromAutoCadPath(string autoCadFilePath)
        {
            string lockFilePath = Path.ChangeExtension(autoCadFilePath, "dwl");
            return File.Exists(lockFilePath) ? lockFilePath : null;
        }
    }
}
