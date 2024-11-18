using ITHit.FileSystem;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using WebDAVDrive.Services;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace WebDAVDrive.Platforms.Windows.Services
{
    public class ToastNotificationService : IToastNotificationService
    {
        private string toastXmlTemplate = @"
                <toast>
                    <visual>
                        <binding template='ToastGeneric'>
                            <image src='ms-appx:///Resources/Images/error.png' placement='appLogoOverride'/>
                            <text>{0}</text>
                            <text>{1}</text>
                        </binding>
                    </visual>
                </toast>";

        public void ShowErrorToast(string title, string message)
        {
            // Create the XML document from the string
            XmlDocument toastXml = new XmlDocument();
            toastXml.LoadXml(string.Format(toastXmlTemplate, title, message));

            // Create the toast notification
            ToastNotification toastNotification = new ToastNotification(toastXml);
            List<string> urls = extractUrls(message);

            if (urls.Count > 0)
            {
                toastNotification.Activated += (_, _) =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = urls[0],
                        UseShellExecute = true
                    });
                };
            }

            // Show the toast notification
            ToastNotificationManager.CreateToastNotifier().Show(toastNotification);
        }

        public void ShowLicenseErrorToast(InvalidLicenseException licenseException)
        {
            ShowErrorToast("License validation failed.", licenseException.Message);
        }

        private List<string> extractUrls(string text)
        {
            // Define the regex pattern for URLs
            string pattern = @"http[s]?://(?:[a-zA-Z]|[0-9]|[$-_@.&+]|[!*\\(\\),]|(?:%[0-9a-fA-F][0-9a-fA-F]))+";

            // Create a regex object
            Regex regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            // Find matches
            MatchCollection matches = regex.Matches(text);

            // Create a list to store URLs
            List<string> urls = new List<string>();

            // Iterate through matches and add to the list
            foreach (Match match in matches)
            {
                urls.Add(match.Value);
            }

            return urls;
        }
    }
}
