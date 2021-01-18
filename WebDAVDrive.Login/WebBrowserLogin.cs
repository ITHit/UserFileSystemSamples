using ITHit.WebDAV.Client;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WebDAVDrive.Login
{
    /// <summary>
    /// Window that contins a web browser 
    /// Web browser is a Microsoft Edge Chromium.
    /// </summary>
    public partial class WebBrowserLogin : Form
    {
        /// <summary>
        /// Succesefull server response or null if no succesefull response was 
        /// obtained (for example the window was closed by the user). 
        /// </summary>
        public IWebResponseAsync Response;

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
        private IWebRequestAsync request;

        /// <summary>
        /// WebDAV Session to make a test request to verify that user has loged-in succesefully.
        /// </summary>
        private WebDavSessionAsync davClient;

        /// <summary>
        ///  Microsoft Edge Chromium instance.
        /// </summary>
        private Microsoft.Web.WebView2.WinForms.WebView2 webView;


        /// <summary>
        /// Current number of 302 redirects.
        /// </summary>
        //private uint numberOfRedirects = 0;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="url">URL to navigate to. This URL must redirect to a log-in page.</param>
        public WebBrowserLogin(Uri url, IWebRequestAsync request, WebDavSessionAsync davClient)
        {
            this.url = url;
            this.request = request;
            this.davClient = davClient;

            InitializeComponent();

            this.SuspendLayout();
            string icoPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), @"Images", "Drive.ico");
            this.Icon = new Icon(icoPath);

            this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();

            this.webView.CreationProperties = null;
            this.webView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.webView.Location = new System.Drawing.Point(0, 0);
            this.webView.Source = this.url;
            this.webView.TabIndex = 0;
            this.webView.ZoomFactor = 1D;
            this.Controls.Add(this.webView);
            this.ResumeLayout(false);

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
        }


        private void CoreWebView2InitializationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2InitializationCompletedEventArgs e)
        {
            webView.CoreWebView2.DocumentTitleChanged += DocumentTitleChanged;
            webView.CoreWebView2.WebResourceResponseReceived += WebResourceResponseReceived;
        }

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
                        await davClient.OpenItemAsync(url);

                        Cookies = netCookies;

                        // Original request completed succesefully. Close the login form.
                        this.Close();
                    }
                    catch
                    {
                        // Request failed. Login failed or did not complete yet.
                    }
                }
            }
        }

        private bool IsSuccess(int statusCode)
        {
            return (statusCode >= 200) && (statusCode <= 299);
        }

        private void DocumentTitleChanged(object sender, object e)
        {
            this.Text = webView.CoreWebView2.DocumentTitle;
        }
    }
}
