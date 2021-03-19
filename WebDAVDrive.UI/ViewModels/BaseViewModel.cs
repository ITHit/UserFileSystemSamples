using System.ComponentModel;

namespace WebDAVDrive.UI.ViewModels
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
