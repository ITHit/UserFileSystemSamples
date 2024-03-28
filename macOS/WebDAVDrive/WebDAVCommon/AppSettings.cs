using System;
using Common.Core;

namespace WebDAVCommon
{
    /// <summary>
    /// Strongly binded project settings.
    /// </summary>
    public class AppSettings : Settings
    {
        /// <summary>
        /// IT Hit WebDAV Client Library for .NET License string;
        /// </summary>
        public string WebDAVClientLicense { get; set; }

        /// <summary>
        /// WebDAV server URLs.
        /// </summary>
        public List<string> WebDAVServerURLs { get; set; } = new();

        /// <summary>
        /// Automatic lock timout in milliseconds.
        /// </summary>
        public double AutoLockTimoutMs { get; set; }

        /// <summary>
        /// Manual lock timout in milliseconds.
        /// </summary>
        public double ManualLockTimoutMs { get; set; }

        /// <summary>
        /// URL to get a thumbnail for Windows Explorer thumbnails mode.
        /// Your server must return 404 Not Found if the thumbnail can not be generated.
        /// If incorrect size is returned, the image will be resized by the platform automatically.
        /// </summary>
        public string ThumbnailGeneratorUrl { get; set; }

        /// <summary>
        /// File types to request thumbnails for.
        /// To request thumbnails for specific file types, list file types using '|' separator.
        /// To request thumbnails for all file types set the value to "*".
        /// </summary>
        public string RequestThumbnailsFor { get; set; }
    }
}

