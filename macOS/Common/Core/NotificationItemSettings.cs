using System;
namespace Common.Core
{
	public class NotificationItemSettings
    {
        /// <summary>
        /// URL of the document to open for editing
        /// </summary>
        public string DocumentUrl { get; set; } = string.Empty;

        /// <summary>
        /// WebDAV server root folder
        /// </summary>
        public string MountUrl { get; set; } = string.Empty;

    }
}

