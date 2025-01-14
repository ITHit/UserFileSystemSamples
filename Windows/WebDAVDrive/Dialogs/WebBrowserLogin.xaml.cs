using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Net;
using Windows.ApplicationModel.Resources;
using Windows.Graphics;
using WinUIEx;

namespace WebDAVDrive.Dialogs
{
    /// <summary>
    /// Web browser dialog, displayed when user must enter credentials in a web browser. 
    /// Enables cookies authentication.
    /// </summary>
    public sealed partial class WebBrowserLogin : DialogWindow
    {
        /// <summary>
        /// URL to navigate to. This URL must redirect to a log-in page.
        /// </summary>
        private readonly Uri url;

        private readonly log4net.ILog log;
        private readonly Action<CookieCollection> loginSucceeded;
        private const int CustomWindowWidth = 600;
        private const int CustomWindowHeight = 800;

        public WebBrowserLogin(Uri url, Action<CookieCollection> loginSucceeded, log4net.ILog log) : base()
        {
            InitializeComponent();

            this.log = log;
            this.url = url;
            this.loginSucceeded = loginSucceeded;

            // Set URL for WebView.
            webView.Source = url;
            webView.NavigationCompleted += WebViewNavigationCompleted;
            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            Title = $"{ServiceProvider.GetService<AppSettings>().ProductName} - {resourceLoader.GetString("WebBrowserLogin/Title")}";

            //set UI parameters; as for this window they are different than for other dialogs - put it here instead of calling base class method
            AppWindow.Resize(new SizeInt32(CustomWindowWidth, CustomWindowHeight));
            CenterWindow();
            this.SetForegroundWindow();
        }

        private async void WebViewNavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                string currentUrl = webView.Source.ToString().TrimEnd('/');
                string targetUrl = url.OriginalString.TrimEnd('/');

                if (string.Equals(currentUrl, targetUrl, StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        IReadOnlyList<CoreWebView2Cookie> cookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync(url.OriginalString);

                        if (cookies.Count > 0)
                        {
                            // Convert WebView2 cookies to .NET CookieCollection
                            CookieCollection netCookies = new CookieCollection();
                            foreach (CoreWebView2Cookie? cookie in cookies)
                            {
                                netCookies.Add(new Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain));
                            }

                            loginSucceeded(netCookies);
                            // Close the dialog.
                            Close();
                        }
                        else
                        {
                            log.Error("Request failed. Login failed or did not complete yet.");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error("Error while retrieving cookies.", ex);
                    }
                }
            }
            else
            {
                log.Error($"Navigation to {webView.Source} failed with error code {args.WebErrorStatus}.");
            }
        }
    }
}
