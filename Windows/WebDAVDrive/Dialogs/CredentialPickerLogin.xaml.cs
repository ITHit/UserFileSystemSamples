using ITHit.FileSystem.Windows.WinUI.Dialogs;
using System;
using System.Net;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Security.Credentials.UI;
using WinUIEx;

namespace WebDAVDrive.Dialogs
{
    /// <summary>
    /// Shows challenge login dialog for Basic, Digest and NTLM/Kerberos auth. 
    /// </summary>
    /// <remarks>This windows shows system login dialog, while displaying icon in a task bar.</remarks>
    public sealed partial class CredentialPickerLogin : DialogWindow
    {
        /// <summary>
        /// URL to navigate to. This URL must redirect to a log-in page.
        /// </summary>
        private readonly Uri url;
        private readonly log4net.ILog log;

        public CredentialPickerLogin(Uri url, log4net.ILog log) : base()
        {
            this.url = url;
            this.log = log;

            InitializeComponent();
            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            Title = $"{ServiceProvider.GetService<AppSettings>().ProductName} - {resourceLoader.GetString("CredentialPickerLogin/Title")}";

            //Set the window transparent, remove minimize and maximize boxes, set other UI parameters.
            //as for this window UI parameters are different than for other dialogs - put it here instead of calling base class method
            Resize(100, 100);
            SystemBackdrop = new TransparentTintBackdrop();
            this.SetExtendedWindowStyle(ExtendedWindowStyle.Layered | ExtendedWindowStyle.Transparent);
            this.SetIsResizable(false);
            this.SetIsMaximizable(false);
            this.SetIsMinimizable(false);
            WindowManager.Get(this).IsTitleBarVisible = false;
            CenterWindow();
            this.SetForegroundWindow();
        }

        public async Task<(NetworkCredential Credential, bool CredentialSaveOption)?> ShowCredentialPickerAsync()
        {
            CredentialPickerResults res;
            CredentialPickerOptions options = new CredentialPickerOptions();
            options.Caption = ServiceProvider.GetService<AppSettings>().ProductName;
            options.CredentialSaveOption = CredentialSaveOption.Unselected;
            options.AuthenticationProtocol = AuthenticationProtocol.Basic;
            options.TargetName = url.OriginalString;
            options.Message = url.OriginalString;

            res = await CredentialPicker.PickAsync(options);

            if (res.ErrorCode == 0)
            {
                return (new NetworkCredential(res.CredentialUserName, res.CredentialPassword), 
                    res.CredentialSaveOption == CredentialSaveOption.Selected);
            }
            return null;
        }
    }
}
