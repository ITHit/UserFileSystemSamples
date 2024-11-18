using Microsoft.Web.WebView2.Core;
using System.Net;

using ITHit.FileSystem.Samples.Common.Windows;
using WebDAVDrive.Extensions;

namespace WebDAVDrive;

public partial class WebBrowserLoginPage : ContentPage
{
    /// <summary>
    /// URL to navigate to. This URL must redirect to a log-in page.
    /// </summary>
    private readonly Uri url;

    private readonly log4net.ILog log;
    private readonly Action<CookieCollection> loginSucceeded;

    public WebBrowserLoginPage(Uri url, Action<CookieCollection> loginSucceeded, log4net.ILog log)
    {
        InitializeComponent();

        this.log = log;
        this.url = url;
        this.loginSucceeded = loginSucceeded;

        // Set URL for WebView.
        webView.Source = url;

        webView.Navigated += WvBrowser_Navigated;
    }

    private async void WvBrowser_Navigated(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Result == WebNavigationResult.Success && e.Url.TrimEnd('/').Equals(url.OriginalString.TrimEnd('/'), StringComparison.InvariantCultureIgnoreCase))
        {
            CookieCollection netCookies = await webView.GetCookies(url);
            if (netCookies.Count != 0)
            {
                loginSucceeded(netCookies);
            }
            else
            {
                log.Error("Request failed. Login failed or did not complete yet.");
            }
        }
    }
}
