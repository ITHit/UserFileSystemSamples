using System.Drawing;
using System.Windows.Forms;

using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows.ShellExtension;

using WebDAVDrive.Platforms.Windows.Utils;
using WebDAVDrive.Services;
using WebDAVDrive.Utils;

namespace WebDAVDrive.Platforms.Windows.Services
{
    internal class DriveTrayIconService
    {
        private readonly Dictionary<Guid, NotifyIcon> trayIcons;

        private readonly IDomainsService domainsService;
        private readonly LogFormatter logFormatter;

        public DriveTrayIconService(IDomainsService domainsService, LogFormatter logFormatter)
        {
            this.logFormatter = logFormatter;
            this.domainsService = domainsService;
            trayIcons = new Dictionary<Guid, NotifyIcon>();
        }

        public void CreateTrayIcon(string driveName, Guid engineKey, VirtualEngine engine)
        {
            MainThread.InvokeOnMainThreadAsync(() =>
            {

                Icon startedIcon = new Icon(Path.Combine(AppContext.BaseDirectory, "Resources/Images/drive.ico"));
                Icon stoppedIcon = new Icon(Path.Combine(AppContext.BaseDirectory, "Resources/Images/drivepause.ico"));

                // Code to create NotifyIcon and ContextMenuStrip here.
                NotifyIcon notifyIcon = new NotifyIcon
                {
                    Icon = startedIcon,
                    Text = $"WebDAV Drive \n {driveName}",
                    Visible = true
                };

                ContextMenuStrip contextMenu = new ContextMenuStrip();
                ToolStripMenuItem debugContextMenu = new ToolStripMenuItem();
                debugContextMenu.Text = "Debug";
                ToolStripMenuItem menuConsole = new ToolStripMenuItem();
                menuConsole.Text = WindowManager.ConsoleVisible ? "Hide log" : "Show log";
                menuConsole.Click += (_, _) =>
                {
                    WindowManager.SetConsoleWindowVisibility(!WindowManager.ConsoleVisible);
                    menuConsole.Text = WindowManager.ConsoleVisible ? "Hide log" : "Show log";
                };
                debugContextMenu.DropDownItems.Add(menuConsole);
                debugContextMenu.DropDownItems.Add("Request Support", null, async (_, _) =>
                {
                    // Submit support tickets, report bugs, suggest features.
                    await Commands.OpenSupportPortalAsync();
                });
                debugContextMenu.DropDownItems.Add("Open Log File", null, (_, _) =>
                {
                    if (logFormatter?.LogFilePath != null && File.Exists(logFormatter?.LogFilePath))
                    {
                        // Open log file.
                        Commands.Open(logFormatter.LogFilePath);
                    }
                });

                // Engine menu items.
                ToolStripItem startStopMenuItem = new ToolStripMenuItem();
                startStopMenuItem.Text = "Stop";

                // Subscribe to the change state event to update the menu item text.
                engine.StateChanged += (_, e) =>
                {
                    if (e.NewState == ITHit.FileSystem.EngineState.Stopped)
                    {
                        startStopMenuItem.Text = "Start";
                        notifyIcon.Icon = stoppedIcon;
                    }
                    else
                    {
                        startStopMenuItem.Text = "Stop";
                        notifyIcon.Icon = startedIcon;
                    }
                };
                startStopMenuItem.Click += async (_, e) =>
                {
                    await engine.Commands.StartStopEngineAsync();
                };

                contextMenu.Items.Add(startStopMenuItem);
                contextMenu.Items.Add("Unmount", null, async (_, _) =>
                {
                    await engine.Commands.StopEngineAsync();
                    await domainsService.UnMountAsync(engineKey, engine.RemoteStorageRootPath);
                });
                contextMenu.Items.Add("Mount New Drive", null, (_, _) =>
                {
                    DialogsUtil.OpenMountNewDriveWindow();
                });
                contextMenu.Items.Add("Exit", null, (_, _) =>
                {
                    Microsoft.Maui.Controls.Application.Current!.Quit();

                });
                contextMenu.Items.Add(new ToolStripSeparator());
                contextMenu.Items.Add(debugContextMenu);
                notifyIcon.ContextMenuStrip = contextMenu;

                trayIcons.Add(engineKey, notifyIcon);
            });
        }

        public void DisposeTrayIcon(Guid engineKey)
        {
            if (trayIcons.ContainsKey(engineKey))
            {
                trayIcons[engineKey].Dispose();

                trayIcons.Remove(engineKey);
            }
        }

    }
}
