using Common.Core;
using ITHit.FileSystem.Mac;
using ITHit.WebDAV.Client;
using ITHit.WebDAV.Client.Exceptions;
using ObjCRuntime;
using WebDAVCommon.Extension;

namespace WebDAVCommon.ViewControllers
{
    public class AuthViewController : NSViewController
    {
        private readonly ConsoleLogger logger = new ConsoleLogger(typeof(AuthViewController).Name);
        private readonly SecureStorage secureStorage;
        private readonly string domainIdentifier;
        private readonly string openItemPath;

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

        private IMacFPUIActionExtensionContext? extensionContext;

        public AuthViewController(string domainIdentifier, IMacFPUIActionExtensionContext extensionContext) : base(nameof(AuthViewController), null)
        {
            this.extensionContext = extensionContext;
            this.domainIdentifier = domainIdentifier;
            this.secureStorage = new SecureStorage(domainIdentifier);
        }

        public AuthViewController(string domainIdentifier, string openItemPath) : base(nameof(AuthViewController), null)
        {
            this.openItemPath = openItemPath;
            this.domainIdentifier = domainIdentifier;
            this.secureStorage = new SecureStorage(domainIdentifier);
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

            if (extensionContext != null)
            {
                view.AddSubview(authenticationButton);
            }

            View = view;

            const int padding = 20;
            if (extensionContext != null)
            {
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
            else
            {
                NSLayoutConstraint.ActivateConstraints(new[] {
                    loginTextField.HeightAnchor.ConstraintEqualTo(25f),
                    loginTextField.LeadingAnchor.ConstraintEqualTo(view.LeadingAnchor, padding),
                    loginTextField.TopAnchor.ConstraintEqualTo(view.TopAnchor, padding),
                    loginTextField.TrailingAnchor.ConstraintEqualTo(view.TrailingAnchor, -padding),

                    passwordTextField.HeightAnchor.ConstraintEqualTo(25f),
                    passwordTextField.LeadingAnchor.ConstraintEqualTo(view.LeadingAnchor, padding),
                    passwordTextField.TopAnchor.ConstraintEqualTo(loginTextField.BottomAnchor, padding),
                    passwordTextField.TrailingAnchor.ConstraintEqualTo(view.TrailingAnchor, -padding)
                });
            }
        }

        public void OnAuthenticationButtonActivated(object? sender, EventArgs e)
        {
            try
            {
                logger.LogDebug($"OnAuthenticationButtonActivated: send signal to file provider.");

                if (ValidateCredentials())
                {
                    if (extensionContext != null)
                    {
                        extensionContext.CompleteRequest();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"OnAuthenticationButtonActivated: {ex.Message}", ex: ex);
            }
        }

        public bool ValidateCredentials()
        {
            string userName = loginTextField.StringValue;
            string password = passwordTextField.StringValue;
            return Task.Run<bool>(async () =>
            {
                await secureStorage.SetAsync("UserName", userName);
                await secureStorage.SetAsync("Password", password);
                await secureStorage.SetAsync("UpdateWebdavSession", "true");

                // Validate credentials.
                if (extensionContext == null)
                {
                    try
                    {
                        using (WebDavSession session = await WebDavSessionUtils.GetWebDavSessionAsync(secureStorage))
                        {
                            await session.GetItemAsync(await secureStorage.GetAsync(domainIdentifier, useDomainIdentifier: false), null, null);
                        }
                    }
                    catch (WebDavHttpException webDavHttpException)
                    {
                        if (webDavHttpException.Status.Code == 401)
                        {
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"ValidateCredentials: {ex.Message}", ex: ex);

                        return false;
                    }
                }

                return true;
            }).Result;

        }
    }
}

