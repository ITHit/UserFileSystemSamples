using System;
using System.IO;
using Common.Core;
using FileProvider;
using Foundation;

namespace WebDAVCommon
{
    public static class AppGroupSettings
    {
        private const string AppGroupId = "${TeamIdentificator}.group.com.webdav.vfs";
        public const string LicenseId = "License";
        public const string WebDAVClientLicenseId = "WebDAVClientLicense";
        public const string WebDAVServerUrlId = "WebDAVServerUrl";
        public const string ThumbnailGeneratorUrlId = "ThumbnailGeneratorUrl";
        public const string RequestThumbnailsForId = "RequestThumbnailsFor";
        public const string WebSocketServerUrlId = "WebSocketServerUrl";

        /// <summary>
        /// Returns remote root path.
        /// </summary>
        public static string GetUserRootPath()
        {
            return NSFileProviderItemIdentifier.RootContainer;
        }

        /// <summary>
        /// Returns license.
        /// </summary>
        public static string GetLicense()
        {
            return BaseAppGroupSettings.GetSettingValue(LicenseId, AppGroupId);
        }

        /// <summary>
        /// Returns WebDAV client license.
        /// </summary>
        public static string GetWebDAVClientLicense()
        {
            return BaseAppGroupSettings.GetSettingValue(WebDAVClientLicenseId, AppGroupId);
        }

        /// <summary>
        /// Returns WebSocket Server Url.
        /// </summary>
        public static string GetWebSocketServerUrl()
        {
            return BaseAppGroupSettings.GetSettingValue(WebSocketServerUrlId, AppGroupId);
        }

        /// <summary>
        /// Returns WebDAV server url.
        /// </summary>
        public static string GetWebDAVServerUrl()
        {
            return BaseAppGroupSettings.GetSettingValue(WebDAVServerUrlId, AppGroupId);
        }

        /// <summary>
        /// Returns thumbnail generator url.
        /// </summary>
        public static string GetThumbnailGeneratorUrl()
        {
            return BaseAppGroupSettings.GetSettingValue(ThumbnailGeneratorUrlId, AppGroupId);
        }

        /// <summary>
        /// Returns thumbnail images extensions.
        /// </summary>
        public static string GetRequestThumbnailsFor()
        {
            return BaseAppGroupSettings.GetSettingValue(RequestThumbnailsForId, AppGroupId);
        }

        /// <summary>
        /// Saves shares settings.
        /// </summary>
        public static NSDictionary SaveSharedSettings(string resourceName)
        {
            return BaseAppGroupSettings.SaveSharedSettings(resourceName, AppGroupId);
        }
    }
}
