using System.Drawing;
using System.Windows.Forms;

using ITHit.FileSystem.Windows.ShellExtension;

using WebDAVDrive.Platforms.Windows.Utils;
using WebDAVDrive.Services;
using WebDAVDrive.Utils;

namespace WebDAVDrive.Platforms.Windows.Services
{
    internal class AppTrayIconService : IDisposable
    {
        private readonly IDomainsService domainsService;

        private NotifyIcon? notifyIcon;

        public AppTrayIconService(IDomainsService domainsService)
        {
            this.domainsService = domainsService;
        }

        public void CreateTrayIcon()
        {
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                notifyIcon = new NotifyIcon
                {
                    Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Resources/Images/drive.ico")),
                    Text = $"WebDAV Drive",
                    Visible = true
                };

                ContextMenuStrip contextMenu = new ContextMenuStrip();

                contextMenu.Items.Add("Mount New Drive", null, (_, _) =>
                {
                    DialogsUtil.OpenMountNewDriveWindow();
                });

                contextMenu.Items.Add("Exit", null, (_, _) =>
                {                   
                    Microsoft.Maui.Controls.Application.Current!.Quit();
                });

                notifyIcon.ContextMenuStrip = contextMenu;
            });
        }

        public void Dispose()
        {
            notifyIcon?.Dispose();
        }
    }
}
