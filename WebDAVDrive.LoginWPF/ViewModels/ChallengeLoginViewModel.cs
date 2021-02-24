using System;
using System.Collections.Generic;
using System.Text;
using System.Security;


namespace WebDAVDrive.LoginWPF.ViewModels
{
    /// <summary>
    /// ViewModel for login window
    /// </summary>
    public class ChallengeLoginViewModel : BaseViewModel
    {
        private string login;
        public string Login 
        {
            get=>login;
            set 
            {
                login = value;
                OnPropertyChanged(nameof(Login));
            } 
        }
        private SecureString password;
        public SecureString Password 
        { 
            get=>password;
            set 
            {
                password = value;
                OnPropertyChanged(nameof(Password));
            }
        }

        private string url;

        /// <summary>
        /// Url of WebDAV server
        /// </summary>
        public string Url 
        {
            get => url;
            set 
            {
                url = value;
                OnPropertyChanged(nameof(Url));
            }
        }

        private bool keepLogedIn;
        /// <summary>
        /// State of KeepLogedIn checkbox
        /// </summary>
        public bool KeepLogedIn
        {
            get => keepLogedIn;
            set
            {
                keepLogedIn = value;
                OnPropertyChanged(nameof(KeepLogedIn));
            }
        }

        /// <summary>
        /// Titile, displayd on head of login window
        /// </summary>
        public string WindowTitle { get; set; }
    }
}
