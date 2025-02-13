using System;

namespace WebDAVDrive.Extensions
{
    /// <summary>
    /// Provides extension methods for user friendly presentations of sizes/progress/etc. in KB/MB/GB
    /// </summary>
    public static class BytesExtension
    {
        /// <summary>
        /// Shows user-friendly message like 5 MB or 15 KB.
        /// </summary>
        /// <param name="bytes">Total number of bytes.</param>
        public static string GetUserFriendlySize(this long bytes)
        {
            if (bytes >= 1024 * 1024 * 1024)
            {
                return $"{Math.Round((double)bytes / (1024 * 1024 * 1024), 0)} GB";
            }
            else if (bytes >= 1024 * 1024)
            {
                return $"{Math.Round((double)bytes / (1024 * 1024), 0)} MB";
            }
            else if (bytes >= 1024)
            {
                return $"{Math.Round((double)bytes / (1024), 0)} KB";
            }
            else
            {
                return $"{bytes} bytes";
            }
        }
    }
}
