using System.Net;
using Windows.Security.Credentials.UI;

namespace WebDAVDrive;

public partial class CredentialPickerLoginPage : ContentPage
{
    /// <summary>
    /// URL to navigate to. This URL must redirect to a log-in page.
    /// </summary>
    private readonly Uri url;
    private readonly string credentialsStorageKey;
    private readonly log4net.ILog log;
    private readonly Action<NetworkCredential> loginSucceeded;
    private readonly Action loginFailed;

    public CredentialPickerLoginPage(Uri url, Action<NetworkCredential> loginSucceeded, Action loginFailed,
                                 string credentialsStorageKey, log4net.ILog log)
    {
        InitializeComponent();
        this.url = url;
        this.loginSucceeded = loginSucceeded;
        this.loginFailed = loginFailed;
        this.credentialsStorageKey = credentialsStorageKey;
        this.log = log;

        this.Loaded += CredentialPickerLogin_Loaded;
    }

    private void CredentialPickerLogin_Loaded(object? sender, EventArgs e)
    {
        _ = MainThread.InvokeOnMainThreadAsync(ShowCredentialPickerAsync);

        //_ = ShowCredentialPickerAsync();
    }

    private async Task ShowCredentialPickerAsync()
    {

        bool succeed = false;
        Windows.Security.Credentials.PasswordCredential passwordCredential = CredentialManager.GetCredentials(credentialsStorageKey, log);
        if (passwordCredential != null)
        {
            passwordCredential.RetrievePassword();
            succeed = true;
            loginSucceeded(new NetworkCredential(passwordCredential.UserName, passwordCredential.Password));
        }
        else
        {
            CredentialPickerResults res;
            CredentialPickerOptions options = new CredentialPickerOptions();
            options.Caption = "WebDAV Drive";
            options.CredentialSaveOption = CredentialSaveOption.Unselected;
            options.AuthenticationProtocol = AuthenticationProtocol.Basic;
            options.TargetName = url.OriginalString;
            options.Message = url.OriginalString;

            res = await CredentialPicker.PickAsync(options);

            if (res.ErrorCode == 0)
            {
                if (res.CredentialSaveOption == CredentialSaveOption.Selected)
                {
                    CredentialManager.SaveCredentials(credentialsStorageKey, res.CredentialUserName, res.CredentialPassword);
                }

                succeed = true;
                loginSucceeded(new NetworkCredential(res.CredentialUserName, res.CredentialPassword));
            }
        }

        if (!succeed)
        {
            loginFailed();
        }
    }
}
