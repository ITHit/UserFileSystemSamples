using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

using Windows.ApplicationModel.Resources;
using Windows.Graphics;

using WebDAVDrive.ViewModels;

namespace WebDAVDrive.Dialogs
{
    /// <summary>
    /// Conflict resolution and files comparison dialog.
    /// </summary>
    public sealed partial class Compare : DialogWindow
    {
        private bool isViewModelLoaded = false;
        private readonly CompareViewModel compareModel;
        private ResourceLoader resourceLoader;

        public Compare(string filePath, VirtualEngine engine) : base()
        {
            InitializeComponent();
            resourceLoader = ResourceLoader.GetForViewIndependentUse();
            Title = $"{ServiceProvider.GetService<AppSettings>().ProductName} - {resourceLoader.GetString("CompareWindow/Title")}";

            gdMain.DataContext = compareModel = new CompareViewModel(filePath, engine);
            // Load the ViewModel only once
            Activated += CompareActivated;

            // Resize and center the window.
            SetDefaultSizePosition();
            // TODO: Set the window size to the size of the screen take into accout scale of text.
            AppWindow.Resize(new SizeInt32(1000, 500));
        }

        private async void CompareActivated(object sender, WindowActivatedEventArgs args)
        {
            // Load the ViewModel only once
            if (!isViewModelLoaded)
            {
                isViewModelLoaded = true;

                // Load last server version of the file
                await compareModel.LoadFileLastServerVersionAsync();

                //focus Close button, make it trigger click by Enter press
                CloseButton.Focus(FocusState.Programmatic);
            }
        }

        private void BtnCloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void BtnTakeServerVersionClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                compareModel.DisableButtons();
                await compareModel.ResolveConflictTakeServerVersionAsync();
                Close();
            }
            catch (Exception ex)
            {
                compareModel.EnableButtons();
                await ShowErrorDialogAsync(ex.Message);
            }
        }

        private async void BtnTakeLocalVersionClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                compareModel.DisableButtons();
                await compareModel.ResolveConflictTakeLocalVersionAsync();
                Close();
            }
            catch (Exception ex)
            {
                compareModel.EnableButtons();
                await ShowErrorDialogAsync(ex.Message);
            }
        }

        private async void BtnCompareClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                compareModel.DisableButtons();
                await compareModel.ResolveConflictMergeAsync();
                Close();
            }
            catch (Exception ex)
            {
                compareModel.EnableButtons();
                await ShowErrorDialogAsync(ex.Message);
            }
        }

        private async Task ShowErrorDialogAsync(string errorMessage)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = resourceLoader.GetString("ErrorContentDialog/Title"),
                Content = errorMessage,
                CloseButtonText = resourceLoader.GetString("ErrorContentDialog/CloseButtonText"),
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}
