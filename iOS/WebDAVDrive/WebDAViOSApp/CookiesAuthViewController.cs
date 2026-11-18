using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Common.Core;
using Foundation;
using ObjCRuntime;
using UIKit;
using WebDAVCommon;
using WebKit;

namespace WebDAViOSApp
{
    /// <summary>
    /// Presents a web view with the server login page and captures the resulting
    /// cookies into the domain secure storage, so the File Provider extension can
    /// attach them to WebDAV requests.
    /// </summary>
    internal class CookiesAuthViewController : UIViewController, IWKNavigationDelegate
    {
        private readonly ConsoleLogger logger = new(nameof(CookiesAuthViewController));
        private readonly SecureStorage secureStorage;
        private readonly string failedUrl;
        private readonly Action? onLoginSuccess;
        private WKWebViewConfiguration? webViewConfiguration;
        private WKWebView? webView;
        private UIActivityIndicatorView? activityIndicator;

        public CookiesAuthViewController(string domainIdentifier, string failedUrl, Action? onLoginSuccess)
        {
            this.failedUrl = failedUrl;
            this.onLoginSuccess = onLoginSuccess;
            secureStorage = new SecureStorage(domainIdentifier);
            Title = $"Sign In - {domainIdentifier}";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                webView?.Dispose();
                webView = null;
                webViewConfiguration?.Dispose();
                webViewConfiguration = null;
                activityIndicator?.Dispose();
                activityIndicator = null;
            }
            base.Dispose(disposing);
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            View!.BackgroundColor = UIColor.SystemBackground;
            NavigationItem.LeftBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Cancel,
                (s, e) => DismissViewController(true, null));

            webViewConfiguration = new WKWebViewConfiguration();
            webView = new WKWebView(View.Bounds, webViewConfiguration)
            {
                TranslatesAutoresizingMaskIntoConstraints = false,
                NavigationDelegate = this
            };
            activityIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large)
            {
                TranslatesAutoresizingMaskIntoConstraints = false,
                HidesWhenStopped = true
            };

            View.AddSubview(webView);
            View.AddSubview(activityIndicator);

            UILayoutGuide safeArea = View.SafeAreaLayoutGuide;
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                webView.TopAnchor.ConstraintEqualTo(safeArea.TopAnchor),
                webView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                webView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                webView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
                activityIndicator.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
                activityIndicator.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor)
            });

            logger.LogDebug($"CookiesAuthViewController loading {failedUrl}");
            webView.LoadRequest(new NSUrlRequest(new NSUrl(failedUrl)));
            activityIndicator.StartAnimating();
        }

        [Export("webView:decidePolicyForNavigationResponse:decisionHandler:")]
        public void DecidePolicy(
            WKWebView webView,
            WKNavigationResponse navigationResponse,
            Action<WKNavigationResponsePolicy> decisionHandler)
        {
            var response = navigationResponse.Response;
            logger.LogDebug($"{nameof(DecidePolicy)} url: {response.Url}");

            if (response is not NSHttpUrlResponse httpUrlResponse ||
                httpUrlResponse.StatusCode < 200 || httpUrlResponse.StatusCode > 299 ||
                !httpUrlResponse.Url.ToString().Equals(failedUrl, StringComparison.InvariantCultureIgnoreCase))
            {
                decisionHandler?.Invoke(WKNavigationResponsePolicy.Allow);
                return;
            }

            // The login page redirected back to the WebDAV URL successfully - harvest the cookies.
            webViewConfiguration!.WebsiteDataStore.HttpCookieStore.GetAllCookies(cookies =>
            {
                logger.LogDebug("CookiesAuthViewController saving cookies");
                Task.Run(async () =>
                {
                    await secureStorage.SetAsync("Cookies",
                        cookies.Select(c => new Cookie(c.Name, c.Value, c.Path, c.Domain)).ToList());
                    await secureStorage.SetAsync("UpdateWebdavSession", "true");

                    InvokeOnMainThread(() =>
                        DismissViewController(true, () => onLoginSuccess?.Invoke()));
                });
            });
            decisionHandler?.Invoke(WKNavigationResponsePolicy.Allow);
        }

        [Export("webView:didFinishNavigation:")]
        public void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
        {
            activityIndicator?.StopAnimating();
        }

        [Export("webView:didFailNavigation:withError:")]
        public void DidFailNavigation(WKWebView webView, WKNavigation navigation, NSError error)
        {
            logger.LogDebug($"{nameof(DidFailNavigation)}: {error.LocalizedDescription}");
            activityIndicator?.StopAnimating();
        }

        [Export("webView:didFailProvisionalNavigation:withError:")]
        public void DidFailProvisionalNavigation(WKWebView webView, WKNavigation navigation, NSError error)
        {
            logger.LogDebug($"{nameof(DidFailProvisionalNavigation)}: {error.LocalizedDescription}");
            activityIndicator?.StopAnimating();
        }
    }
}
