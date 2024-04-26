using System;
using System.Net;
using Common.Core;
using ITHit.FileSystem;
using ITHit.WebDAV.Client;

namespace WebDAVCommon
{
	public class WebDavSessionUtils
	{
        /// <summary>
        /// Initializes WebDAV session.
        /// </summary>
        public static async Task<WebDavSession> GetWebDavSessionAsync(SecureStorage secureStorage)
        {
            WebDavSession webDavSession = new WebDavSession(AppGroupSettings.Settings.Value.WebDAVClientLicense);
            webDavSession.CustomHeaders.Add("InstanceId", Environment.MachineName);

            await UpdateAuthenticationAsync(webDavSession, secureStorage);
            return webDavSession;
        }

        /// <summary>
        /// Updates authentication settings.
        /// </summary>
        /// <param name="webDavSession">WebDav session.</param>
        /// <returns></returns>
        public static async Task UpdateAuthenticationAsync(WebDavSession webDavSession, SecureStorage secureStorage)
        {
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
                    CookieCollection currentCookies = webDavSession.CookieContainer.GetAllCookies();
                    foreach (Cookie cookie in cookies)
                    {
                        Cookie? currentCookie = webDavSession.CookieContainer.GetAllCookies().Where(p => p.Name == cookie.Name && p.Domain == cookie.Domain).FirstOrDefault();
                        if (currentCookie == null)
                        {
                            webDavSession.CookieContainer.Add(new Cookie(cookie.Name, cookie.Value)
                            {
                                Domain = cookie.Domain,
                                Secure = cookie.Secure,
                                HttpOnly = cookie.HttpOnly
                            });
                        }
                        else
                        {
                            currentCookie.Value = cookie.Value;
                        }
                    }
                }
            }
        }
    }
}

