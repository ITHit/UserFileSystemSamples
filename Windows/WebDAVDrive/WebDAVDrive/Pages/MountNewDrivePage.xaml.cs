using System.Text.RegularExpressions;
using WebDAVDrive.Services;
using WebDAVDrive.Utils;

namespace WebDAVDrive;

public partial class MountNewDrivePage : ContentPage
{
    public MountNewDrivePage()
    {
        InitializeComponent();
    }

    private async void OnValidateClicked(object sender, EventArgs e)
    {
        string url = UrlEntry.Text;

        // Check if the URL entry is empty
        if (string.IsNullOrWhiteSpace(url))
        {
            // Show required validation message
            RequiredMessage.IsVisible = true;
            ValidationMessage.IsVisible = false;
        }
        else
        {
            // Hide the required validation message
            RequiredMessage.IsVisible = false;

            // Validate the URL format
            if (IsValidUrl(url))
            {
                ValidationMessage.IsVisible = false;
                btnAddDrive.IsEnabled = false;

                // Mount new domain.
                _ = Task.Run(async () =>
                {
                    await ServiceProviderUtil.GetService<IDomainsService>().MountNewAsync([url]);

                });
                Application.Current.CloseWindow(Window);
            }
            else
            {
                ValidationMessage.Text = "Invalid URL. Please enter a valid URL.";
                ValidationMessage.IsVisible = true;
            }
        }
    }

    private bool IsValidUrl(string url)
    {
        // Simple URL validation regex
        string pattern = @"^https?://([\w-]+(\.[\w-]+)+)(:[0-9]+)?(/[\w- ./?%&=]*)?$";
        return Regex.IsMatch(url, pattern);
    }

    private void OnCloseClicked(object sender, EventArgs e)
    {
        Application.Current.CloseWindow(Window);
    }
}
