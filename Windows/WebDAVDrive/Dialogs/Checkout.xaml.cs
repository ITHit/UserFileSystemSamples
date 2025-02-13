using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;

using WebDAVDrive.Extensions;

namespace WebDAVDrive.Dialogs;

/// <summary>
/// A checkout dialog window.
/// </summary>
public sealed partial class Checkout : DialogWindow
{
    private CheckoutManager manager;
    private string remoteStoragePath;
    private string lockToken;

    public Checkout(CheckoutManager manager, string userFileSystemPath, string remoteStoragePath, string lockToken): base()
    {
        this.manager = manager;
        this.remoteStoragePath = remoteStoragePath;
        this.lockToken = lockToken;
        InitializeComponent();
        this.Resize(800, 400);
        ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
        Title = $"{ServiceProvider.GetService<AppSettings>().ProductName} - {resourceLoader.GetString("Checkout/Content")}";

        lblFileName.Text = $"{resourceLoader.GetString("Name")}: {Path.GetFileName(userFileSystemPath)}";
        lblOpenFromDomain.Text = $"{resourceLoader.GetString("From")}: {new Uri(remoteStoragePath).Host}";

        // Center the window.
        SetDefaultPosition();
    }

    private async void OnCheckoutClicked(object sender, RoutedEventArgs e)
    {
        // Checkout remote file.
        bool isCheckoutSuccess = await manager.CheckOutAsync(remoteStoragePath, lockToken);
        if (isCheckoutSuccess)
        {
            lblResult.Text = "CheckOut Success";
            await Task.Delay(1000);
            Close();
        }
        else
        {
            lblResult.Text = "CheckOut Failed";
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
       Close();
    }
}
