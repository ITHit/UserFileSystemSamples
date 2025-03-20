using Microsoft.UI.Xaml;
using WebDAVDrive.Services;
using Windows.ApplicationModel.Resources;

namespace WebDAVDrive.Dialogs
{
    /// <summary>
    /// New drive mounting dialog.
    /// </summary>
    public sealed partial class Settings : DialogWindow
    {
        private readonly ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();

        private readonly VirtualEngine engine;

        public Settings(VirtualEngine engine) : base()
        {
            InitializeComponent();
            Resize(700, 600);
            this.engine = engine;
            Title = $"{ServiceProvider.GetService<AppSettings>().ProductName} - {resourceLoader.GetString("SettingsWindow/Title")}";

            // Resize and center the window.
            SetDefaultPosition();

            //Set values from engine
            AutomaticLockTimeout.Text = (engine.AutoLockTimeoutMs / 1000).ToString();
            ManualLockTimeout.Text = engine.ManualLockTimeoutMs == -1 ? string.Empty : (engine.ManualLockTimeoutMs / 1000).ToString();
            AutoLockEnable.IsOn = engine.AutoLock;
            ReadOnlyOnLockedFiles.IsOn = engine.SetLockReadOnly;
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            bool isValidationError = false;
            AutomaticRequiredMessage.Visibility = AutomaticValidationMessage.Visibility = ManualValidationMessage.Visibility = Visibility.Collapsed;

            //"Automatic lock timeout" field is required and should be a number
            if (string.IsNullOrWhiteSpace(AutomaticLockTimeout.Text))
            {
                isValidationError = true;
                AutomaticRequiredMessage.Visibility = Visibility.Visible;
                AutomaticValidationMessage.Visibility = Visibility.Collapsed;
            }
            else if (!double.TryParse(AutomaticLockTimeout.Text, out double automaticLockTimeout))
            {
                isValidationError = true;
                AutomaticRequiredMessage.Visibility = Visibility.Collapsed;
                AutomaticValidationMessage.Visibility = Visibility.Visible;
            }

            //"Manual lock timeout" field is NOT required, but if provided - it should be a number
            if (!string.IsNullOrWhiteSpace(ManualLockTimeout.Text) && !double.TryParse(ManualLockTimeout.Text, out double manualLockTimeout))
            {
                isValidationError = true;
                ManualValidationMessage.Visibility = Visibility.Visible;
            }

            if (!isValidationError)
            {
                UserSettingsService userSettingsService = ServiceProvider.GetService<UserSettingsService>();
                userSettingsService.SaveSettings(engine, new UserSettings
                {
                    AutomaticLockTimeout = double.Parse(AutomaticLockTimeout.Text) * 1000,
                    ManualLockTimeout = string.IsNullOrWhiteSpace(ManualLockTimeout.Text) ? -1 : (double.Parse(ManualLockTimeout.Text) * 1000),
                    SetLockReadOnly = ReadOnlyOnLockedFiles.IsOn,
                    AutoLock = AutoLockEnable.IsOn
                });                

                Close();
            }
        }
    }
}
