using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Windows.ApplicationModel.Resources;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows.WinUI.ViewModels;

using WebDAVDrive.ViewModels;
using ITHit.FileSystem.Windows.WinUI.Dialogs;


namespace WebDAVDrive.Dialogs
{
    /// <summary>
    /// Error Details dialog.
    /// </summary>
    public sealed partial class ErrorDetails : DialogWindow
    {
        private ResourceLoader resourceLoader;
        private readonly LogFormatter logFormatter;

        public ErrorDetails(FileEventViewModel eventMovel, VirtualEngine? engine, LogFormatter logFormatter) : base()
        {
            this.logFormatter = logFormatter;
            InitializeComponent();
            Resize(1000, 700);
            resourceLoader = ResourceLoader.GetForViewIndependentUse();
            Title = $"{ServiceProvider.GetService<AppSettings>().ProductName} - {resourceLoader.GetString("ErrorDetailsWindow/Title")}";
            string notApplicable = resourceLoader.GetString("NotApplicable");

            // Center the window.
            SetDefaultPosition();

            //set error details
            FilePath.Text = eventMovel.InitialPath;
            if (eventMovel.OperationType == ITHit.FileSystem.OperationType.MoveCompletion)
            {
                TargetPath.Text = eventMovel.Path;
                TargetPath.Visibility = TargetPathLabel.Visibility = Visibility.Visible;
            }
            RemoteStoragePath.Text = engine?.Mapping?.MapPath(eventMovel.Path) ?? notApplicable;
            Message.Text = string.IsNullOrEmpty(eventMovel.ErrorMessage) ? notApplicable : eventMovel.ErrorMessage;
            SyncDirection.Text = eventMovel.Direction.ToString();
            OperationStatus.Text = eventMovel.OperationStatus.ToString();
            OperationType.Text = eventMovel.OperationType.ToString();
            ComponentName.Text = eventMovel.ComponentName;
            NotificationTime.Text = eventMovel.NotificationTime.ToString();
            ExceptionStackTrace.Text = string.IsNullOrEmpty(eventMovel.ExceptionStackTrace) ? notApplicable : eventMovel.ExceptionStackTrace;

            //set history details
            ObservableCollection<HistoryItemViewModel> historyModels = new ObservableCollection<HistoryItemViewModel>();
            foreach (OperationResult historyItem in eventMovel.History)
            {
                historyModels.Add(new HistoryItemViewModel
                {
                    ErrorMessage = historyItem.Exception?.Message ?? historyItem.Message ?? notApplicable,
                    OperationStatus = historyItem.Status,
                    ExceptionStackTrace = historyItem.Exception?.StackTrace ?? notApplicable
                });
            }
            HistoryItems.DataContext = historyModels;
            HistoryLabel.Visibility = historyModels.Any() ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void RequestSupportClicked(object sender, RoutedEventArgs e)
        {
            await Commands.OpenSupportPortalAsync();
        }

        private void OpenLogClicked(object sender, RoutedEventArgs e)
        {
            // Open log file.
            Commands.TryOpen(logFormatter.LogFilePath);
        }

        private void BtnCloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        //focus Close button, make it trigger click by Enter press
        private void CloseButtonLoaded(object sender, RoutedEventArgs e)
        {
            CloseButton.Focus(FocusState.Programmatic);
        }
    }
}
