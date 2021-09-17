using ITHit.WebDAV.Client;
using log4net;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Windows;

namespace WebDAVDrive.UI
{
    /// <summary>
    /// Interaction logic for WebBrowserLogin.xaml
    /// </summary>
    public partial class WebBrowserLogin : Window
    {
        /// <summary>
        /// Succesefull server response or null if no succesefull response was 
        /// obtained (for example the window was closed by the user). 
        /// </summary>
        public IWebResponse Response;

        /// <summary>
        /// Web browser cookies.
        /// </summary>
        public CookieCollection Cookies;

        /// <summary>
        /// URL to navigate to. This URL must redirect to a log-in page.
        /// </summary>
        private Uri url;

        /// <summary>
        /// WebDAV request to send to the server. It will typically redirect tot the login page.
        /// </summary>
        private IWebRequest request;

        /// <summary>
        /// WebDAV Client to make a test request to verify that user has loged-in succesefully.
        /// </summary>
        private WebDavSession davClient;

        /// <summary>
        ///  Microsoft Edge Chromium instance.
        /// </summary>
        private Microsoft.Web.WebView2.Wpf.WebView2 webView;

        /// <summary>
        /// Task for initializign of WebView2
        /// </summary>
        private Task WebViewInitializeTask;

        /// <summary>
        /// Navigation events count
        /// </summary>
        private int CountOfNavigationStartingEvents;

        /// <summary>
        /// Delegete for navigating to WebDAV server URL
        /// </summary>
        Delegate DoNavigation;

        private ILog log;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="url">URL to navigate to. This URL must redirect to a log-in page.</param>
        public WebBrowserLogin(Uri url, WebDavSession davClient, ILog log)
        {
            this.url = url;
            this.davClient = davClient;
            this.log = log;
            InitializeComponent();

            this.webView = new Microsoft.Web.WebView2.Wpf.WebView2();
            this.Loaded += WebBrowserLogin_Load;
            this.panel.Children.Add(this.webView);
        }

        /// <summary>
        /// WebBrowserLogin Load event handler
        /// </summary>
        public void WebBrowserLogin_Load(object sender, EventArgs e)
        {
            DoNavigation = (Action)delegate
            {
                webView.Source = new Uri(this.url.ToString(), UriKind.Absolute);
            };
            CountOfNavigationStartingEvents = 0;
            webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2Ready;
            WebViewInitializeTask = webView.EnsureCoreWebView2Async();
        }

        /// <summary>
        /// CoreWebView2InitializationCompleted event handler
        /// </summary>
        private void WebView_CoreWebView2Ready(object sender, EventArgs e)
        {
            webView.CoreWebView2.DocumentTitleChanged += DocumentTitleChanged;
            webView.CoreWebView2.WebResourceResponseReceived += WebResourceResponseReceived;
            WebViewInitializeTask.ContinueWith((t) =>
            {
                webView.NavigationStarting += WebView_NavigationStarting;
                try
                {
                    webView.Dispatcher.Invoke(DoNavigation);
                }
                catch (Exception e)
                {
                    log.Error($"WebView navigation failed: {e.Message}");
                }
            });
        }
        /// <summary>
        /// CoreWebView2Initialization event handler
        /// </summary>
        private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            CountOfNavigationStartingEvents += 1;
        }

        /// <summary>
        /// WebResourceResponseReceived event hanlder
        /// </summary>
        private async void WebResourceResponseReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            // Here we test if the login is succeseful resending the original request to the server.
            // You may want to check the return URL or rely on the number of redirects instead of resending the request.

            if (e.Response != null)
            {
                int statusCode = e.Response.StatusCode;

                if (IsSuccess(statusCode)
                    && e.Request.Uri.Equals(url.OriginalString, StringComparison.InvariantCultureIgnoreCase)
                    && e.Request.Method == "GET")
                {
                    // Read cookies.
                    List<CoreWebView2Cookie> webViewcookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync(url.OriginalString);
                    if (webViewcookies.Count != 0)
                    {
                        CookieCollection netCookies = new CookieCollection();
                        webViewcookies.ForEach((x) => { netCookies.Add(x.ToSystemNetCookie()); });

                        // Copy cookies from web browser window into original  request.
                        //request.CookieContainer.Add(netCookies);

                        try
                        {
                            // Test if the original request works after we set cookies.
                            // We do not want the error event to fire in this case.
                            //Response = await request.GetResponseAsync(false);

                            davClient.CookieContainer.Add(netCookies);
                            // Can't validate cookies the following way, as it triggers a continues Login dialog appear if cookies are incorrect
                            //await davClient.GetItemAsync(url);

                            Cookies = netCookies;

                            // Original request completed succesefully. Close the login form.
                            this.Close();
                        }
                        catch
                        {
                            log.Error("Request failed. Login failed or did not complete yet.");
                        }
                    }
                    else
                    {
                        log.Error("Request failed. Login failed or did not complete yet.");
                    }
                }
            }
        }

        private bool IsSuccess(int statusCode)
        {
            return (statusCode >= 200) && (statusCode <= 299);
        }

        /// <summary>
        /// Event handler which changes window title acording to document title
        /// </summary>
        private void DocumentTitleChanged(object sender, object e)
        {
            Title = Title+" - "+webView.CoreWebView2.DocumentTitle;
        }
    }


}
