using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.System;

using WebDAVDrive.Services;
using System.Collections.Generic;
using System.Linq;
using ITHit.FileSystem.Windows.WinUI.Dialogs;

namespace WebDAVDrive.Dialogs
{
    /// <summary>
    /// New drive mounting dialog.
    /// </summary>
    public sealed partial class MountNewDrive : DialogWindow
    {
        private readonly ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
        public MountNewDrive() : base()
        {
            InitializeComponent();
            Resize(800, 400);
            Title = $"{ServiceProvider.GetService<AppSettings>().ProductName} - {resourceLoader.GetString("MountNewDriveWindow/Title")}";

            // Resize and center the window.
            SetDefaultPosition();
        }

        private void OnValidateClicked(object sender, RoutedEventArgs e)
        {
            string url = UrlEntry.Text;

            // Check if the URL entry is empty
            if (string.IsNullOrWhiteSpace(url))
            {
                // Show required validation message
                RequiredMessage.Visibility = Visibility.Visible;
                ValidationMessage.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Hide the required validation message
                RequiredMessage.Visibility = Visibility.Collapsed;

                // Validate the URL format
                if (IsValidUrl(url))
                {
                    IDrivesService drivesService = ServiceProvider.GetService<IDrivesService>();
                    ValidationMessage.Visibility = Visibility.Collapsed;
                    btnAddDrive.IsEnabled = false;

                    // Mount new domain.
                    _ = Task.Run(async () =>
                    {
                        (bool success, Exception? exception) result = await drivesService.MountNewAsync(url);

                        if (result.success)
                        {
                            ServiceProvider.DispatcherQueue.TryEnqueue(() =>
                            {
                                Close();
                            });
                        }
                        else
                        {
                            // Unmount engine if mounting failed.
                            KeyValuePair<Guid, VirtualEngine>? engine = drivesService.Engines.Where(p => p.Value.RemoteStorageRootPath == url).FirstOrDefault();
                            if (engine != null)
                            {
                                await drivesService.UnMountAsync(engine.Value.Value.InstanceId);
                            }

                            ServiceProvider.DispatcherQueue.TryEnqueue(async () =>
                            {
                                ContentDialog dialog = new ContentDialog
                                {
                                    Title = resourceLoader.GetString("ErrorContentDialog/Title"),
                                    Content = result.exception?.Message,
                                    CloseButtonText = resourceLoader.GetString("ErrorContentDialog/CloseButtonText"),
                                    XamlRoot = Content.XamlRoot
                                };

                                await dialog.ShowAsync();
                                btnAddDrive.IsEnabled = true;
                            });
                        }
                    });
                }
                else
                {
                    ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
                    ValidationMessage.Text = resourceLoader.GetString("InvalidUrl");
                    ValidationMessage.Visibility = Visibility.Visible;
                }
            }
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private bool IsValidUrl(string url)
        {
            // Simple URL validation regex
            string pattern = @"^https?://([\w-]+(\.[\w-]+)+)(:[0-9]+)?(/[\w- ./?%&=]*)?$";
            return Regex.IsMatch(url, pattern);
        }

        //focus UrlEntry textbox, make it trigger click by Enter press
        private void UrlEntryLoaded(object sender, RoutedEventArgs e)
        {
            UrlEntry.Focus(FocusState.Programmatic);
        }

        //"click" Add button on Enter (being inside textbox focused)
        private void UrlEntryKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                OnValidateClicked(btnAddDrive, null);
            }
        }
    }
}
