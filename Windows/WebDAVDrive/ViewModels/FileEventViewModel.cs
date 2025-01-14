using Microsoft.UI.Xaml.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WebDAVDrive.ViewModels
{
    /// <summary>
    /// View model representing event with some file - to show it as row in Tray window.
    /// </summary>
    public class FileEventViewModel : ObservableObject
    {
        private BitmapImage thumbnail;

        /// <summary>
        /// Holds the file thumbnail.
        /// </summary>
        public BitmapImage Thumbnail
        {
            get { return thumbnail; }
            set
            {
                SetProperty(ref thumbnail, value);
            }
        }

        private string fileName;

        /// <summary>
        /// File name related with the event.
        /// </summary>
        public string FileName
        {
            get { return fileName; }
            set
            {
                SetProperty(ref fileName, value);
            }
        }

        private string eventNameText;

        /// <summary>
        /// Event name, e.g. Deleted, Sent, Moved.
        /// </summary>
        public string EventNameText
        {
            get { return eventNameText; }
            set
            {
                SetProperty(ref eventNameText, value);
            }
        }

        private string timeText;

        /// <summary>
        /// Time of the event, e.g. 8:25 PM.
        /// </summary>
        public string TimeText
        {
            get { return timeText; }
            set
            {
                SetProperty(ref timeText, value);
            }
        }
    }
}
