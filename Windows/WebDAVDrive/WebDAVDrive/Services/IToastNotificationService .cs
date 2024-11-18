using ITHit.FileSystem;

namespace WebDAVDrive.Services
{
    public interface IToastNotificationService
    {
        void ShowLicenseErrorToast(InvalidLicenseException ex);

        void ShowErrorToast(string title, string message);
    }
}
