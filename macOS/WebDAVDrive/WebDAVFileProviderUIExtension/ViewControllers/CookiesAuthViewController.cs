using System.Net;
using Common.Core;
using ITHit.FileSystem.Mac;
using ObjCRuntime;
using WebDAVCommon;
using WebKit;

namespace WebDAVFileProviderUIExtension.ViewControllers
{
    public class CookiesAuthViewController : NSViewController, IWKNavigationDelegate
    {
        private readonly IMacFPUIActionExtensionContext extensionContext;
        private readonly ConsoleLogger consoleLogger = new(nameof(CookiesAuthViewController));
        private readonly SecureStorage secureStorage = new();
        private readonly string failedUrl;
        private WKWebViewConfiguration webViewConfiguration;
        private NSProgressIndicator progressIndicator = new()
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            ControlSize = NSControlSize.Large,
            ControlTint = NSControlTint.Blue
        };
        private WKWebView webView;

        public CookiesAuthViewController(IMacFPUIActionExtensionContext extensionContext, string failedUrl)
                : base(nameof(CookiesAuthViewController), null)
        {
            consoleLogger.LogDebug("CookiesAuthViewController constructor");
            this.extensionContext = extensionContext;
            this.failedUrl = failedUrl;
            consoleLogger.LogDebug("CookiesAuthViewController init all parameters.");
        }

        protected CookiesAuthViewController(NativeHandle handle) : base(handle)
        {
            // This constructor is required if the view controller is loaded from a xib or a storyboard.
            // Do not put any initialization here, use ViewDidLoad instead.
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (webView != null)
                {
                    webView.Dispose();
                    webView = null;
                }
                if (webViewConfiguration != null)
                {
                    webViewConfiguration.Dispose();
                    webViewConfiguration = null;
                }
                if (progressIndicator != null)
                {
                    progressIndicator.Dispose();
                    progressIndicator = null;
                }
            }
            base.Dispose(disposing);
        }

        public override void LoadView()
        {
            consoleLogger.LogDebug("CookiesAuthViewController LoadView");
            NSView view = new NSView()
            {
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            webViewConfiguration = new WKWebViewConfiguration();
            webView = new WKWebView(new CGRect(0, 0, 250, 250), webViewConfiguration)
            {
                TranslatesAutoresizingMaskIntoConstraints = false,
                NavigationDelegate = this
            };
            view.AddSubview(webView);
            view.AddSubview(progressIndicator);
            View = view;
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                webView.TopAnchor.ConstraintEqualTo(view.TopAnchor),
                webView.LeadingAnchor.ConstraintEqualTo(view.LeadingAnchor),
                webView.TrailingAnchor.ConstraintEqualTo(view.TrailingAnchor),
                webView.BottomAnchor.ConstraintEqualTo(view.BottomAnchor),
                progressIndicator.CenterXAnchor.ConstraintEqualTo(view.CenterXAnchor),
                progressIndicator.CenterYAnchor.ConstraintEqualTo(view.CenterYAnchor)
            });

            consoleLogger.LogDebug("CookiesAuthViewController finish LoadView");
        }

        public override void ViewDidLoad()
        {
            consoleLogger.LogDebug($"CookiesAuthViewController ViewDidLoad {failedUrl}");
            base.ViewDidLoad();         
            var url = new NSUrl(failedUrl);
            var request = new NSUrlRequest(url);
            webView.LoadRequest(request);
            progressIndicator.StartAnimation(this);
        }

        [Export("webView:decidePolicyForNavigationResponse:decisionHandler:")]
        public void DecidePolicy(
            WKWebView webView,
            WKNavigationResponse navigationResponse,
            Action<WKNavigationResponsePolicy> decisionHandler)
        {
            var response = navigationResponse.Response;
            consoleLogger.LogDebug($"{nameof(DecidePolicy)} url: {response.Url}");
            if (response is not NSHttpUrlResponse httpUrlResponse ||
                !CheckIsSuccessStatusCode(httpUrlResponse.StatusCode) ||
                !httpUrlResponse.Url.ToString().Equals(failedUrl, StringComparison.InvariantCultureIgnoreCase))
            {
                consoleLogger.LogDebug($"CookiesAuthViewController WKNavigationResponsePolicy.Allow");
                decisionHandler?.Invoke(WKNavigationResponsePolicy.Allow);
                return;
            }
            webViewConfiguration.WebsiteDataStore.HttpCookieStore.GetAllCookies(cookies =>
            {
                consoleLogger.LogDebug($"CookiesAuthViewController set cookies");
                secureStorage.SetAsync("RequireAuthentication", "").Wait();
                secureStorage.SetAsync("Cookies", cookies.Select(c => new Cookie(c.Name, c.Value, c.Path, c.Domain)).ToList()).Wait();            
                extensionContext.CompleteRequest();
            });
            decisionHandler?.Invoke(WKNavigationResponsePolicy.Allow);

            bool CheckIsSuccessStatusCode(nint statusCode)
            {
                return statusCode >= 200 && statusCode <= 299;
            }
        }

        [Export("webView:didStartProvisionalNavigation:")]
        public void DidStartProvisionalNavigation(WKWebView webView, WKNavigation navigation)
        {
            consoleLogger.LogDebug($"{nameof(DidStartProvisionalNavigation)}");
        }

        [Export("webView:didFinishNavigation:")]
        public void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
        {
            consoleLogger.LogDebug($"{nameof(DidFinishNavigation)}");
            progressIndicator.StopAnimation(this);
        }

        [Export("webView:didFailNavigation:withError:")]
        public void DidFailNavigation(WKWebView webView, WKNavigation navigation, NSError error)
        {
            consoleLogger.LogDebug($"{nameof(DidFailNavigation)}");
            progressIndicator.StopAnimation(this);
        }
    }
}

