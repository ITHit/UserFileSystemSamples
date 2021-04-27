using System;
using System.Collections.Generic;
using System.Text;
using System.Security;


namespace WebDAVDrive.UI.ViewModels
{
    /// <summary>
    /// ViewModel for login window
    /// </summary>
    public class ConnectViewModel : BaseViewModel
    {
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

        /// <summary>
        /// Titile, displayd on head of login window
        /// </summary>
        public string WindowTitle { get; set; }
    }
}
