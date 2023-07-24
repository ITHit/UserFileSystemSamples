using System;
using System.IO;
using Common.Core;
using FileProvider;
using Foundation;

namespace WebDAVCommon
{
    public static class AppGroupSettings
    {
        public const string UserFileSystemLicenseId = "UserFileSystemLicense";
        public const string WebDAVClientLicenseId = "WebDAVClientLicense";
        public const string WebDAVServerUrlId = "WebDAVServerUrl";
        public const string ThumbnailGeneratorUrlId = "ThumbnailGeneratorUrl";
        public const string RequestThumbnailsForId = "RequestThumbnailsFor";
        public const string WebSocketServerUrlId = "WebSocketServerUrl";

        /// <summary>
        /// Returns license.
        /// </summary>
        public static string GetLicense()
        {
            return BaseAppGroupSettings.GetSettingValue(UserFileSystemLicenseId);
        }

        /// <summary>
        /// Returns WebDAV client license.
        /// </summary>
        public static string GetWebDAVClientLicense()
        {
            return BaseAppGroupSettings.GetSettingValue(WebDAVClientLicenseId);
        }

        /// <summary>
        /// Returns WebSocket Server Url.
        /// </summary>
        public static string GetWebSocketServerUrl()
        {
            return BaseAppGroupSettings.GetSettingValue(WebSocketServerUrlId);
        }

        /// <summary>
        /// Returns WebDAV server url.
        /// </summary>
        public static string GetWebDAVServerUrl()
        {
            return BaseAppGroupSettings.GetSettingValue(WebDAVServerUrlId);
        }

        /// <summary>
        /// Returns thumbnail generator url.
        /// </summary>
        public static string GetThumbnailGeneratorUrl()
        {
            return BaseAppGroupSettings.GetSettingValue(ThumbnailGeneratorUrlId);
        }

        /// <summary>
        /// Returns thumbnail images extensions.
        /// </summary>
        public static string GetRequestThumbnailsFor()
        {
            return BaseAppGroupSettings.GetSettingValue(RequestThumbnailsForId);
        }
    }
}
