using Microsoft.UI.Xaml;
using System.IO;
using Windows.ApplicationModel.Resources;

using ITHit.FileSystem.Samples.Common;

using WebDAVDrive.Extensions;

namespace WebDAVDrive.Dialogs
{
    /// <summary>
    /// A checkin dialog window.
    /// </summary>
    public sealed partial class Checkin : DialogWindow
    {
        private CheckoutManager manager;
        private ServerLockInfo lockInfo;
        private string RemoteStoragePath;

        public Checkin(CheckoutManager manager, string userFileSystemPath, string remoteStoragePath, ServerLockInfo lockInfo)
        {
            this.manager = manager;
            this.RemoteStoragePath = remoteStoragePath;
            this.lockInfo = lockInfo;
            InitializeComponent();
            this.Resize(800, 400);
            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            Title = $"{ServiceProvider.GetService<AppSettings>().ProductName} - {resourceLoader.GetString("Checkin/Content")}";

            lblFileName.Text = $"{resourceLoader.GetString("Checkin_Question/Text")}: {Path.GetFileName(userFileSystemPath)}";

            // Center the window.
            SetDefaultPosition();
        }

        private async void OnCheckinClicked(object sender, RoutedEventArgs e)
        {
            // Checkin remote file.
            bool isCheckinSuccess = await manager.CheckInAsync(RemoteStoragePath, eComments.Text, lockInfo);
            if (isCheckinSuccess)
            {
                lblResult.Text = "CheckIn Success";
                if (cbKeep.IsChecked == true)
                    await manager.CheckOutAsync(RemoteStoragePath, lockInfo.LockToken);
                Close();
            }
            else
            {
                lblResult.Text = "CheckIn Failed";
            }
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
