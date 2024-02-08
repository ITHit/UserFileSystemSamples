using System;
namespace WebDAVCommon
{
	public class DomainSettings
	{
        /// <summary>
        /// WebDAV server URL.
        /// </summary>
        public string WebDAVServerUrl { get; set; } = string.Empty;

        /// <summary>
        /// WebSocket server URL.
        /// </summary>
        public string WebSocketServerUrl { get; set; } = string.Empty;
    }
}

