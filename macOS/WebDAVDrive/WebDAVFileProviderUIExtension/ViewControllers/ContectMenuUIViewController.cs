using Common.Core;
using ITHit.FileSystem.Mac;
using ObjCRuntime;
using WebDAVCommon.Extension;
using WebDAVCommon.ViewControllers;

namespace WebDAVFileProviderUIExtension.ViewControllers
{
    public class ContectMenuUIViewController : NSViewController
    {    
        private ConsoleLogger logger = new ConsoleLogger(typeof(AuthViewController).Name);

        private NSTextField loginTextField = new()
        {
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        private NSButton closeButton = new()
        {
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        private IMacFPUIActionExtensionContext extensionContext;

        public ContectMenuUIViewController(IMacFPUIActionExtensionContext extensionContext) : base(nameof(AuthViewController), null)
        {
            this.extensionContext = extensionContext;
        }

        protected ContectMenuUIViewController(NativeHandle handle) : base(handle)
        {
            // This constructor is required if the view controller is loaded from a xib or a storyboard.
            // Do not put any initialization here, use ViewDidLoad instead.
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (loginTextField != null)
                {
                    loginTextField.Dispose();
                    loginTextField = null;
                }
               

                if (closeButton != null)
                {
                    closeButton.Dispose();
                    closeButton = null;
                }
            }

            base.Dispose(disposing);
        }

        public override void LoadView()
        {
            NSView view = new NSView()
            {
                TranslatesAutoresizingMaskIntoConstraints = false
            };

            closeButton.Activated += OnCloseButtonActivated;

            loginTextField.SetBackgroundColor(NSColor.White);            
            closeButton.SetBackgroundColor(NSColor.White);

            loginTextField.Cell.PlaceholderAttributedString = "Custom Textbox".CreateAttributedString(NSColor.Gray);           
            closeButton.SetColoredTitle("Close", NSColor.Black);

            loginTextField.TextColor = NSColor.Black;

            view.AddSubview(loginTextField);
            view.AddSubview(closeButton);

            View = view;

            const int padding = 20;

            NSLayoutConstraint.ActivateConstraints(new[] {
                loginTextField.HeightAnchor.ConstraintEqualTo(25f),
                loginTextField.LeadingAnchor.ConstraintEqualTo(view.LeadingAnchor, padding),
                loginTextField.TopAnchor.ConstraintEqualTo(view.TopAnchor, padding),
                loginTextField.TrailingAnchor.ConstraintEqualTo(view.TrailingAnchor, -padding),             

                closeButton.HeightAnchor.ConstraintEqualTo(25f),
                closeButton.CenterXAnchor.ConstraintEqualTo(view.CenterXAnchor),
                closeButton.LeadingAnchor.ConstraintGreaterThanOrEqualTo(view.LeadingAnchor, padding * 3),
                closeButton.TrailingAnchor.ConstraintGreaterThanOrEqualTo(view.TrailingAnchor, -padding),
                closeButton.BottomAnchor.ConstraintEqualTo(view.BottomAnchor, -padding)
            });
        }

        private void OnCloseButtonActivated(object? sender, EventArgs e)
        {
            try
            {
                logger.LogDebug($"OnCloseButtonActivated: send signal to file provider.");           
                extensionContext.CompleteRequest();
            }
            catch(Exception ex)
            {
                logger.LogError($"OnCloseButtonActivated: {ex.Message}", ex: ex);
            }
        }
    }
}

