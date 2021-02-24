using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;

namespace WebDAVDrive.LoginWPF.ViewModels
{
    /// <summary>
    /// Base MVVM ViewModel class
    /// </summary>
    public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName) 
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
