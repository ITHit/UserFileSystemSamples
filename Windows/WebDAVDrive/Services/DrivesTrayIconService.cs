using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Windows.ApplicationModel.Resources;
using Icon = System.Drawing.Icon;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;

using WebDAVDrive.Dialogs;


namespace WebDAVDrive.Services
{
    /// <summary>
    /// Manages list of drive tray icons. 
    /// Allows creation and deletion drive trays.
    /// </summary>
    internal class DrivesTrayIconService
    {
        private readonly Dictionary<Guid, NotifyIcon> trayIcons;

        private readonly IDrivesService domainsService;
        private readonly LogFormatter logFormatter;

        public DrivesTrayIconService(IDrivesService domainsService, LogFormatter logFormatter)
        {
            this.logFormatter = logFormatter;
            this.domainsService = domainsService;
            trayIcons = new Dictionary<Guid, NotifyIcon>();
        }

        public void CreateTrayIcon(string driveName, Guid engineKey, VirtualEngine engine)
        {
            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            ServiceProvider.DispatcherQueue.TryEnqueue(() =>
            {
                // Create Tray window.
                Tray trayWindow = CreateTrayWindow(engineKey, engine);

                Icon startedIcon = new Icon(Path.Combine(AppContext.BaseDirectory, "Images/drive.ico"));
                Icon stoppedIcon = new Icon(Path.Combine(AppContext.BaseDirectory, "Images/drivepause.ico"));
                Icon syncIcon = new Icon(Path.Combine(AppContext.BaseDirectory, "Images/drivesync.ico"));


                // Code to create NotifyIcon and ContextMenuStrip here.
                NotifyIcon notifyIcon = new NotifyIcon
                {
                    Icon = startedIcon,
                    Text = $"{ServiceProvider.GetService<AppSettings>().ProductName}\n{driveName}",
                    Visible = true
                };

                // Subscribe to the change state event to update the menu item text.
                engine.StateChanged += (_, e) =>
                {
                    if (e.NewState == EngineState.Stopped)
                    {                        
                        notifyIcon.Icon = stoppedIcon;
                        notifyIcon.Text = $"{ServiceProvider.GetService<AppSettings>().ProductName}\n{driveName} - {resourceLoader.GetString("Idle")}";
                    }
                    else
                    {                       
                        notifyIcon.Icon = startedIcon;
                        notifyIcon.Text = $"{ServiceProvider.GetService<AppSettings>().ProductName}\n{driveName} - {resourceLoader.GetString("SynchronizationStopped")}";
                    }
                };

                // Subscribe to the SyncService change state event to update the menu item text.
                engine.SyncService.StateChanged += (_, e) =>
                {
                    if (e.NewState ==  SynchronizationState.Synchronizing)
                    {
                        notifyIcon.Icon = syncIcon;
                        notifyIcon.Text = $"{ServiceProvider.GetService<AppSettings>().ProductName}\n{driveName} - {resourceLoader.GetString("Synching")}";
                    }
                    else
                    {
                        notifyIcon.Icon = startedIcon;
                        notifyIcon.Text = $"{ServiceProvider.GetService<AppSettings>().ProductName}\n{driveName} - {resourceLoader.GetString("Idle")}";
                    }
                };

                notifyIcon.Click += (_, _) =>
                {
                    //in case Tray window is not pinned right now - show it with animation
                    if (!trayWindow.Pinned)
                    {
                        trayWindow.ShowWithAnimation();
                    }
                };

                trayIcons.Add(engineKey, notifyIcon);
            });
        }

        public Tray CreateTrayWindow(Guid engineKey, VirtualEngine engine)
        {
            Tray trayWindow = new Tray(engineKey, engine, domainsService, logFormatter);
            trayWindow.SetInitialVisualParameters();
            return trayWindow;
        }

        public void RemoveTrayIcon(Guid engineKey)
        {
            if (trayIcons.ContainsKey(engineKey))
            {
                trayIcons[engineKey].Dispose();
                trayIcons.Remove(engineKey);
            }
        }

    }
}
