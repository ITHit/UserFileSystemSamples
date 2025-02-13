using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

using ITHit.FileSystem;

using WebDAVDrive.Extensions;

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

        private BitmapImage mainOverlayIcon;

        /// <summary>
        /// Holds the main overlay icon showing kind of event ("up" for upload, "down" for download, lock/unlock, "plus" for created item).
        /// </summary>
        public BitmapImage MainOverlayIcon
        {
            get { return mainOverlayIcon; }
            set
            {
                SetProperty(ref mainOverlayIcon, value);
                OnPropertyChanged(nameof(MainOverlayIconVisibility));
            }
        }

        /// <summary>
        /// Indicates whether we should show main overlay icon (it is true if icon is defined).
        /// </summary>
        public Visibility MainOverlayIconVisibility
        {
            get { return mainOverlayIcon == null ? Visibility.Collapsed : Visibility.Visible; }
        }

        private BitmapImage syncTypeOverlayIcon;

        /// <summary>
        /// Holds the additional overlay icon showing whether operation is incoming or outgoing.
        /// </summary>
        public BitmapImage SyncTypeOverlayIcon
        {
            get { return syncTypeOverlayIcon; }
            set
            {
                SetProperty(ref syncTypeOverlayIcon, value);
            }
        }

        private string path;

        /// <summary>
        /// File or folder path related with the event.
        /// </summary>
        public string Path
        {
            get { return path; }
            set
            {
                SetProperty(ref path, value);
            }
        }

        private SyncDirection direction;

        /// <summary>
        /// File or folder path related with the event.
        /// </summary>
        public SyncDirection Direction
        {
            get { return direction; }
            set
            {
                SetProperty(ref direction, value);
                OnPropertyChanged(nameof(EventNameText));
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

        private OperationType operationType;

        /// <summary>
        /// Operation type(e.g. OperationType.Delete or OperationType.Create)
        /// </summary>
        public OperationType OperationType
        {
            get { return operationType; }
            set
            {
                SetProperty(ref operationType, value);
                OnPropertyChanged(nameof(EventNameText));
            }
        }

        /// <summary>
        /// User-friendly event name text, e.g. Download, Deletion or Creation
        /// </summary>
        public string EventNameText
        {
            get { return operationType.GetFriendlyName(direction); }
        }

        private string progressText;

        /// <summary>
        /// Progress text, e.g. 12.5 MB of 25.7 MB.
        /// </summary>
        public string ProgressText
        {
            get { return progressText; }
            set
            {
                SetProperty(ref progressText, value);
            }
        }

        private long progressPercent;

        /// <summary>
        /// Progress percent
        /// </summary>
        public long ProgressPercent
        {
            get { return progressPercent; }
            set
            {
                SetProperty(ref progressPercent, value);
            }
        }

        private Visibility progressVisibility;

        /// <summary>
        /// Progress visibility
        /// </summary>
        public Visibility ProgressVisibility
        {
            get { return progressVisibility; }
            set
            {
                SetProperty(ref progressVisibility, value);
                OnPropertyChanged(nameof(TimeVisibility));
            }
        }

        private Visibility eventNameVisibility;

        /// <summary>
        /// Event name visibility
        /// </summary>
        public Visibility EventNameVisibility
        {
            get { return eventNameVisibility; }
            set
            {
                SetProperty(ref eventNameVisibility, value);
            }
        }

        public Visibility TimeVisibility
        {
            get { return progressVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; }
        }

        private DateTime time;

        /// <summary>
        /// Time of the event.
        /// </summary>
        public DateTime Time
        {
            get { return time; }
            set
            {
                SetProperty(ref time, value);
                OnPropertyChanged(nameof(TimeText));
            }
        }

        /// <summary>
        /// User-friendly string representing event's time.
        /// </summary>
        public string TimeText
        {
            get { return time.ToString("h:mm tt"); }
        }

        private bool isCompleted;

        /// <summary>
        /// Is used for Incoming UpdateContent and Populate events. Shows if operation is completed.
        /// For other types of events it is always true.
        /// </summary>
        public bool IsCompleted
        {
            get { return isCompleted; }
            set
            {
                SetProperty(ref isCompleted, value);
            }
        }

        private string? errorMessage;

        /// <summary>
        /// Is filled for operations with error. Contains error message. 
        /// For successed events it is null.
        /// </summary>
        public string? ErrorMessage
        {
            get { return errorMessage; }
            set
            {
                SetProperty(ref errorMessage, value);
                OnPropertyChanged(nameof(ErrorTooltipVisibility));
            }
        }

        public Visibility ErrorTooltipVisibility
        {
            get { return string.IsNullOrEmpty(errorMessage) ? Visibility.Collapsed : Visibility.Visible; }
        }
    }
}
