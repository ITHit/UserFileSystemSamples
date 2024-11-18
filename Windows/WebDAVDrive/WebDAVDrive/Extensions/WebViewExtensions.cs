using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System.Net;

namespace WebDAVDrive.Extensions
{
    public static class WebViewExtensions
    {
        public static async Task<CookieCollection> GetCookies(this WebView webView, Uri uri)
        {
            return await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                WebView2 webView2 = webView?.Handler?.PlatformView as WebView2;
                CoreWebView2CookieManager cookieManager = webView2?.CoreWebView2.CookieManager;
                if (cookieManager == null)
                {
                    return new CookieCollection();
                }

                IReadOnlyList<CoreWebView2Cookie> cookies = await cookieManager.GetCookiesAsync(uri.ToString());
                CookieCollection cookieCollection = new CookieCollection();
                foreach (var cookie in cookies)
                {
                    cookieCollection.Add(new System.Net.Cookie
                    {
                        Name = cookie.Name,
                        Value = cookie.Value,
                        Domain = cookie.Domain,
                        Path = cookie.Path,
                        Expires = DateTime.MinValue,
                        HttpOnly = cookie.IsHttpOnly,
                        Secure = cookie.IsSecure
                    });
                }

                return cookieCollection;
            });
        }
    }
}
