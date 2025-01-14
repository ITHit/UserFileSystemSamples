using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WebDAVDrive.ViewModels
{
    /// <summary>
    /// View model representing file events to show on Settings window.
    /// </summary>
    public class FilesEventsViewModel : ObservableObject
    {
        /// <summary>
        /// Collection of file events to show on Settings window.
        /// </summary>
        public ObservableCollection<FileEventViewModel> LineItems { get; private set; } = new();
    }
}
