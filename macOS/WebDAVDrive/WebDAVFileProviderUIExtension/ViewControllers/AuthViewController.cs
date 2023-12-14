using Common.Core;
using ITHit.FileSystem.Mac;
using ObjCRuntime;
using WebDAVCommon;
using WebDAVFileProviderUIExtension.Extension;

namespace WebDAVFileProviderUIExtension.ViewControllers
{
    public class AuthViewController : NSViewController
    {    
        private ConsoleLogger logger = new ConsoleLogger(typeof(AuthViewController).Name);
        private SecureStorage secureStorage = new SecureStorage();

        private NSTextField loginTextField = new()
        {
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        private NSSecureTextField passwordTextField = new()
        {
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        private NSButton authenticationButton = new()
        {
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        private IMacFPUIActionExtensionContext extensionContext;

        public AuthViewController(IMacFPUIActionExtensionContext extensionContext) : base(nameof(AuthViewController), null)
        {
            this.extensionContext = extensionContext;
        }

        protected AuthViewController(NativeHandle handle) : base(handle)
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

                if (passwordTextField != null)
                {
                    passwordTextField.Dispose();
                    passwordTextField = null;
                }

                if (authenticationButton != null)
                {
                    authenticationButton.Dispose();
                    authenticationButton = null;
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

            authenticationButton.Activated += OnAuthenticationButtonActivated;

            loginTextField.SetBackgroundColor(NSColor.White);
            passwordTextField.SetBackgroundColor(NSColor.White);
            authenticationButton.SetBackgroundColor(NSColor.White);

            loginTextField.Cell.PlaceholderAttributedString = "Login".CreateAttributedString(NSColor.Gray);
            passwordTextField.Cell.PlaceholderAttributedString = "Password".CreateAttributedString(NSColor.Gray);
            authenticationButton.SetColoredTitle("Authenticate", NSColor.Black);

            loginTextField.TextColor = NSColor.Black;
            passwordTextField.TextColor = NSColor.Black;


            view.AddSubview(loginTextField);
            view.AddSubview(passwordTextField);
            view.AddSubview(authenticationButton);

            View = view;

            const int padding = 20;

            NSLayoutConstraint.ActivateConstraints(new[] {
                loginTextField.HeightAnchor.ConstraintEqualTo(25f),
                loginTextField.LeadingAnchor.ConstraintEqualTo(view.LeadingAnchor, padding),
                loginTextField.TopAnchor.ConstraintEqualTo(view.TopAnchor, padding),
                loginTextField.TrailingAnchor.ConstraintEqualTo(view.TrailingAnchor, -padding),

                passwordTextField.HeightAnchor.ConstraintEqualTo(25f),
                passwordTextField.LeadingAnchor.ConstraintEqualTo(view.LeadingAnchor, padding),
                passwordTextField.TopAnchor.ConstraintEqualTo(loginTextField.BottomAnchor, padding),
                passwordTextField.TrailingAnchor.ConstraintEqualTo(view.TrailingAnchor, -padding),

                authenticationButton.HeightAnchor.ConstraintEqualTo(25f),
                authenticationButton.CenterXAnchor.ConstraintEqualTo(view.CenterXAnchor),
                authenticationButton.LeadingAnchor.ConstraintGreaterThanOrEqualTo(view.LeadingAnchor, padding * 3),
                authenticationButton.TrailingAnchor.ConstraintGreaterThanOrEqualTo(view.TrailingAnchor, -padding),
                authenticationButton.TopAnchor.ConstraintEqualTo(passwordTextField.BottomAnchor, padding * 3),
                authenticationButton.BottomAnchor.ConstraintEqualTo(view.BottomAnchor, -padding)
            });
        }

        private void OnAuthenticationButtonActivated(object? sender, EventArgs e)
        {
            try
            {
                logger.LogDebug($"OnAuthenticationButtonActivated: send signal to file provider.");

                secureStorage.SetAsync("LoginType", "UserNamePassword").Wait();
                secureStorage.SetAsync("UserName", loginTextField.StringValue).Wait();
                secureStorage.SetAsync("Password", passwordTextField.StringValue).Wait();              
                extensionContext.CompleteRequest();
            }
            catch(Exception ex)
            {
                logger.LogError($"OnAuthenticationButtonActivated: {ex.Message}", ex: ex);
            }
        }
    }
}

