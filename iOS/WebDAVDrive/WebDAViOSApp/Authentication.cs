using System;
using System.Threading.Tasks;
using Common.Core;
using ITHit.FileSystem;
using ITHit.WebDAV.Client;
using ITHit.WebDAV.Client.Exceptions;
using UIKit;
using WebDAVCommon;

namespace WebDAViOSApp
{
    /// <summary>
    /// Presents the authentication UI required by a mounted domain, mirroring the
    /// macOS container app login dialog. The File Provider extension sets the
    /// LoginType / RequireAuthentication flags in the domain secure storage when
    /// the server responds with 401 (basic/digest) or a 302 redirect (cookies).
    /// </summary>
    internal static class Authentication
    {
        private static readonly ILogger logger = new ConsoleLogger(nameof(Authentication));

        /// <summary>
        /// Returns the login type ("UserNamePassword" or "Cookies") when the domain
        /// requires authentication, otherwise null.
        /// </summary>
        public static async Task<string?> GetRequiredLoginTypeAsync(string domainIdentifier)
        {
            SecureStorage secureStorage = new SecureStorage(domainIdentifier);
            return string.IsNullOrEmpty(await secureStorage.GetAsync("RequireAuthentication"))
                ? null
                : await secureStorage.GetAsync("LoginType");
        }

        /// <summary>
        /// Presents the appropriate login UI for the domain: a credentials dialog
        /// for basic/digest auth, or a web view with the login page for cookies auth.
        /// </summary>
        public static async Task PresentAsync(UIViewController presenter, string domainIdentifier, Action? onSuccess = null)
        {
            SecureStorage secureStorage = new SecureStorage(domainIdentifier);
            string loginType = await secureStorage.GetAsync("LoginType");

            switch (loginType)
            {
                case "UserNamePassword":
                    PresentCredentialsDialog(presenter, secureStorage, domainIdentifier, onSuccess);
                    break;

                case "Cookies":
                    // Load the URL that redirected to the login page; fall back to the drive root.
                    string failedUrl = await secureStorage.GetAsync("CookiesFailedUrl");
                    if (string.IsNullOrEmpty(failedUrl))
                    {
                        failedUrl = await secureStorage.GetAsync(domainIdentifier, useDomainIdentifier: false);
                    }

                    var cookiesController = new CookiesAuthViewController(domainIdentifier, failedUrl, onSuccess);
                    var navigation = new UINavigationController(cookiesController)
                    {
                        ModalPresentationStyle = UIModalPresentationStyle.PageSheet
                    };
                    presenter.PresentViewController(navigation, true, null);
                    break;
            }
        }

        private static void PresentCredentialsDialog(
            UIViewController presenter,
            SecureStorage secureStorage,
            string domainIdentifier,
            Action? onSuccess,
            string? errorMessage = null)
        {
            var alert = UIAlertController.Create(
                "Sign In Required",
                errorMessage ?? $"Enter your credentials for {domainIdentifier}",
                UIAlertControllerStyle.Alert);

            alert.AddTextField(field =>
            {
                field.Placeholder = "User name";
                field.AutocapitalizationType = UITextAutocapitalizationType.None;
                field.AutocorrectionType = UITextAutocorrectionType.No;
                field.KeyboardType = UIKeyboardType.Default;
            });
            alert.AddTextField(field =>
            {
                field.Placeholder = "Password";
                field.SecureTextEntry = true;
            });

            alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
            alert.AddAction(UIAlertAction.Create("Sign In", UIAlertActionStyle.Default, async _ =>
            {
                string userName = alert.TextFields[0].Text?.Trim() ?? string.Empty;
                string password = alert.TextFields[1].Text ?? string.Empty;

                if (await ValidateCredentialsAsync(secureStorage, domainIdentifier, userName, password))
                {
                    onSuccess?.Invoke();
                }
                else
                {
                    PresentCredentialsDialog(presenter, secureStorage, domainIdentifier,
                        onSuccess, "Invalid credentials. Please try again.");
                }
            }));

            presenter.PresentViewController(alert, true, null);
        }

        /// <summary>
        /// Stores credentials and validates them against the WebDAV server,
        /// same as the macOS AuthViewController.ValidateCredentials().
        /// </summary>
        private static async Task<bool> ValidateCredentialsAsync(
            SecureStorage secureStorage, string domainIdentifier, string userName, string password)
        {
            await secureStorage.SetAsync("UserName", userName);
            await secureStorage.SetAsync("Password", password);
            await secureStorage.SetAsync("UpdateWebdavSession", "true");

            try
            {
                using WebDavSession session = await WebDavSessionUtils.GetWebDavSessionAsync(secureStorage);
                await session.GetItemAsync(await secureStorage.GetAsync(domainIdentifier, useDomainIdentifier: false), null, null);
                return true;
            }
            catch (WebDavHttpException webDavHttpException)
            {
                if (webDavHttpException.Status.Code != 401)
                {
                    logger.LogError($"ValidateCredentialsAsync: {webDavHttpException.Message}", ex: webDavHttpException);
                }
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError($"ValidateCredentialsAsync: {ex.Message}", ex: ex);
                return false;
            }
        }
    }
}
