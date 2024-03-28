using System;
using System.Net;
using ITHit.WebDAV.Client;

namespace WebDAVCommon
{
	public class WebDavSessionUtils
	{
        /// <summary>
        /// Initializes WebDAV session.
        /// </summary>
        public static async Task<WebDavSession> GetWebDavSessionAsync()
        {
            SecureStorage secureStorage = new SecureStorage();
            WebDavSession webDavSession = new WebDavSession(AppGroupSettings.Settings.Value.WebDAVClientLicense);
            webDavSession.CustomHeaders.Add("InstanceId", Environment.MachineName);

            string loginType = await secureStorage.GetAsync("LoginType");
            if (!string.IsNullOrEmpty(loginType) && loginType.Equals("UserNamePassword"))
            {
                webDavSession.Credentials = new NetworkCredential(await secureStorage.GetAsync("UserName"), await secureStorage.GetAsync("Password"));
            }
            else if (!string.IsNullOrEmpty(loginType) && loginType.Equals("Cookies"))
            {
                List<Cookie> cookies = await secureStorage.GetAsync<List<Cookie>>("Cookies");
                if (cookies != null)
                {
                    foreach (Cookie cookie in cookies)
                    {
                        webDavSession.CookieContainer.Add(cookie);
                    }
                }
            }

            return webDavSession;
        }
    }
}

