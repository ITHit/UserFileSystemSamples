using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Windows.ApplicationModel.Resources;

using WebDAVDrive.Dialogs;
using WinUIEx;

namespace WebDAVDrive.Services
{
    /// <summary>
    /// Default tray icon, displayed when no drives are mounted. 
    /// Required to display "Mount new Drive" menu.
    /// </summary>
    internal class DefaultTrayIcon : IDisposable
    {
        private readonly IDrivesService domainsService;

        private NotifyIcon? notifyIcon;

        public DefaultTrayIcon(IDrivesService domainsService)
        {
            this.domainsService = domainsService;
        }

        public void CreateTrayIcon()
        {
            ServiceProvider.DispatcherQueue.TryEnqueue(() =>
            {
                ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
                notifyIcon = new NotifyIcon
                {
                    Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Images/drive.ico")),
                    Text = ServiceProvider.GetService<AppSettings>().ProductName,
                    Visible = true
                };

                ContextMenuStrip contextMenu = new ContextMenuStrip();

                contextMenu.Items.Add(resourceLoader.GetString("MountNewDrive/Text"), null, (_, _) =>
                {
                    // Open the MountNewDrive dialog
                    new MountNewDrive().Show();
                });

                contextMenu.Items.Add(resourceLoader.GetString("Exit/Text"), null, (_, _) =>
                {
                    Microsoft.UI.Xaml.Application.Current.Exit();
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
