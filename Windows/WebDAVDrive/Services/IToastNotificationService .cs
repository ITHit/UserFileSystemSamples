using ITHit.FileSystem;

namespace WebDAVDrive.Services
{
    /// <summary>
    /// Windows notifications displayed on the right side of the screen.
    /// </summary>
    public interface IToastNotificationService
    {
        void ShowLicenseError(InvalidLicenseException ex);

        void ShowError(string title, string message);
    }
}
