using ITHit.FileSystem.Windows.WinUI.Dialogs;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Net;
using Windows.ApplicationModel.Resources;
using Windows.UI.WebUI;
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
        private readonly bool clearCookies;
        private bool cookiesCleared = false; //to make cookies clearing only once, before user logs in

        public WebBrowserLogin(Uri url, Action<CookieCollection> loginSucceeded, log4net.ILog log, bool clearCookies = false) : base()
        {
            InitializeComponent();

            this.log = log;
            this.url = url;
            this.loginSucceeded = loginSucceeded;
            this.clearCookies = clearCookies;

            // Set URL for WebView.
            webView.Source = url;
            webView.NavigationCompleted += WebViewNavigationCompleted;
            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            Title = $"{ServiceProvider.GetService<AppSettings>().ProductName} - {resourceLoader.GetString("WebBrowserLogin/Title")}";

            //set UI parameters; as for this window they are different than for other dialogs - put it here instead of calling base class method
            Resize(600, 800);
            CenterWindow();
            this.SetForegroundWindow();
        }

        private async void WebViewNavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                string currentUrl = webView.Source.ToString().TrimEnd('/');
                string targetUrl = url.OriginalString.TrimEnd('/');

                if (clearCookies && !cookiesCleared)
                {
                    //in case URL is the same as targetUrl - it means user logged in, so we really should clear cookies and reload
                    if (currentUrl.Contains(targetUrl, StringComparison.InvariantCultureIgnoreCase))
                    {
                        IReadOnlyList<CoreWebView2Cookie> cookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync(url.OriginalString);
                        foreach (CoreWebView2Cookie? cookie in cookies)
                        {
                            webView.CoreWebView2.CookieManager.DeleteCookie(cookie);
                        }
                        cookiesCleared = true;
                        //reload page with cleared cookies - to show login page for user and then perform proper login
                        webView.Source = url;
                        webView.Reload();
                        return;
                    }
                    //otherwise (login URL, so user is not logged in) - we just set cookiesCleared to true, to avoid further clearing
                    else
                    {
                        cookiesCleared = true;
                    }
                }

               // For some url I saw currentUrl has tail like /Forms/AllItems.aspx and Equals() fails to compare.
               // if (string.Equals(currentUrl, targetUrl, StringComparison.InvariantCultureIgnoreCase))
                if (currentUrl.Contains(targetUrl, StringComparison.InvariantCultureIgnoreCase))
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
