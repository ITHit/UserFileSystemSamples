using System;
using System.IO;
using System.Windows.Forms;
using Windows.ApplicationModel.Resources;
using WinUIEx;

using ITHit.FileSystem.Samples.Common.Windows;
using WindowManager = ITHit.FileSystem.Samples.Common.Windows.WindowManager;

using WebDAVDrive.Dialogs;

namespace WebDAVDrive.Services
{
    /// <summary>
    /// Default tray icon, displayed when no drives are mounted. 
    /// Required to display "Mount new Drive" menu.
    /// </summary>
    internal class DefaultTrayIcon : IDisposable
    {
        private readonly IDrivesService domainsService;
        private readonly LogFormatter logFormatter;

        private NotifyIcon? notifyIcon;

        public DefaultTrayIcon(IDrivesService domainsService, LogFormatter logFormatter)
        {
            this.domainsService = domainsService;
            this.logFormatter = logFormatter;
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

                contextMenu.Items.Add(resourceLoader.GetString("RequestSupport/Text"), null, async (_, _) =>
                {
                    // Open request support URL
                    await Commands.OpenSupportPortalAsync();
                });

                contextMenu.Items.Add(resourceLoader.GetString("MountNewDrive/Text"), null, (_, _) =>
                {
                    // Open the MountNewDrive dialog
                    new MountNewDrive().Show();
                });

                ToolStripMenuItem debugMenu = new ToolStripMenuItem(resourceLoader.GetString("DebugMenu/Text"));

                //Hides/Shows console log
                ToolStripMenuItem hideShowLog = new ToolStripMenuItem(resourceLoader.GetString("HideLog"));
                hideShowLog.Click += (_, _) =>
                {
                    WindowManager.SetConsoleWindowVisibility(!WindowManager.ConsoleVisible);
                    hideShowLog.Text = WindowManager.ConsoleVisible ? resourceLoader.GetString("HideLog") : resourceLoader.GetString("ShowLog");
                };
                debugMenu.DropDownItems.Add(hideShowLog);

                //Enables/disables debug logging
                ToolStripMenuItem enableDisableDebugLogging = new ToolStripMenuItem(logFormatter.DebugLoggingEnabled ?
                    resourceLoader.GetString("DisableDebugLogging") : resourceLoader.GetString("EnableDebugLogging"));
                enableDisableDebugLogging.Click += (_, _) =>
                {
                    logFormatter.DebugLoggingEnabled = !logFormatter.DebugLoggingEnabled;
                    enableDisableDebugLogging.Text = logFormatter.DebugLoggingEnabled ?
                        resourceLoader.GetString("DisableDebugLogging") : resourceLoader.GetString("EnableDebugLogging"); ;
                };
                debugMenu.DropDownItems.Add(enableDisableDebugLogging);

                //Opens log file
                debugMenu.DropDownItems.Add(resourceLoader.GetString("OpenLogFile/Text"), null, (_, _) =>
                {
                    if (logFormatter?.LogFilePath != null && File.Exists(logFormatter?.LogFilePath))
                    {
                        Commands.TryOpen(logFormatter.LogFilePath);
                    }
                });

                contextMenu.Items.Add(debugMenu);
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
