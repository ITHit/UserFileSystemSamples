using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Samples.Common.Windows.Syncronyzation;
using static ITHit.FileSystem.Samples.Common.Windows.Syncronyzation.FullSyncService;

namespace WebDAVDrive.UI
{
    /// <summary>
    /// Represents tray application.
    /// </summary>
    public class WindowsTrayInterface
    {
        /// <summary>
        /// Create new tray icon.
        /// </summary>s
        /// <param name="productName">Product name.</param>
        /// <param name="virtualDrive">VirtualDriveBase, need to get syncService and fileSystemMonitor.</param>
        /// <param name="exitEvent">ManualResetEvent, used to stop application.</param>
        /// <returns></returns>
        public static Thread CreateTrayInterface(string productName, IVirtualDrive virtualDrive, ConsoleManager.ConsoleExitEvent exitEvent) 
        {
            // Start tray application.
            Thread thread = new Thread(() => {
                WindowsTrayInterface windowsTrayInterface = new WindowsTrayInterface($"{productName}", virtualDrive);
                Application.Run();
                exitEvent.Set();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.IsBackground = true;
            return thread;
        }

        /// <summary>
        /// Changes button status to Idle.
        /// </summary>
        private void StatusToIdle() 
        {
            notifyIcon.Text = Title + $"\n{Localization.Resources.Idle}";
            notifyIcon.ContextMenuStrip.Items[0].Text = Localization.Resources.StopSync;
            notifyIcon.Icon = new System.Drawing.Icon("Images\\Drive.ico"); ;
        }

        /// <summary>
        /// Changes button status to Synching.
        /// </summary>
        private void StatusToSynching() 
        {
            notifyIcon.Text = Title + $"\n{Localization.Resources.StatusSync}";
            notifyIcon.ContextMenuStrip.Items[0].Text = Localization.Resources.StopSync;
            notifyIcon.Icon = new System.Drawing.Icon("Images\\DriveSync.ico"); ;
        }

        private void StatusToSyncStopped() 
        {
            notifyIcon.Text = Title + $"\n{Localization.Resources.StatusSyncStopped}";
            notifyIcon.ContextMenuStrip.Items[0].Text = Localization.Resources.StartSync;
            notifyIcon.Icon = new System.Drawing.Icon("Images\\DrivePause.ico"); ;
        }
        /// <summary>
        /// Icon in the status bar notification area.
        /// </summary>
        public NotifyIcon notifyIcon;

        /// <summary>
        /// Visibility of notify icon.
        /// </summary>
        public static bool Visible = true;

        /// <summary>
        /// Notify icon title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Icon click handler delegete
        /// </summary>
        public delegate void ItemClickHanler();

        /// <summary>
        /// Creates a tray application instance.
        /// </summary>
        /// <param name="title">Tray application title.</param>
        /// <param name="syncService">
        /// Synchronization service instance. The tray application will enable/disable this application and show its status.
        /// </param>
        public WindowsTrayInterface(string title, IVirtualDrive virtualDrive) 
        {
            Title = title;
            notifyIcon = new NotifyIcon();

            notifyIcon.Visible = true;
            notifyIcon.Icon = new System.Drawing.Icon("Images\\Drive.ico");
            notifyIcon.Text = title;

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add(Localization.Resources.StopSync, null, (s, e) => { StartStopSync(virtualDrive); });
#if !DEBUG
            // Hide console on app start.
            Visible = false;
            ConsoleManager.SetConsoleWindowVisibility(false);
            contextMenu.Items.Add(Localization.Resources.ShowLog, null, (s, e) => {
#else
            contextMenu.Items.Add(Localization.Resources.HideLog, null, (s, e) => {
#endif
                Visible = !Visible;
                ConsoleManager.SetConsoleWindowVisibility(Visible);
                contextMenu.Items[1].Text = (Visible)? Localization.Resources.HideLog : Localization.Resources.ShowLog;
            });

            contextMenu.Items.Add($"{Localization.Resources.Exit} {title}",null, (s,e) => { Application.Exit(); });

            notifyIcon.ContextMenuStrip = contextMenu;
        }

        /// <summary>
        /// Defines StartStop sync button in tray app.
        /// </summary>
        private static bool sycnStopped = false;

        /// <summary>
        /// This method handles StartStop Sycn button in tray menu.
        /// </summary>
        private async void StartStopSync(IVirtualDrive virtualDrive)
        {
            if (!sycnStopped)
            {
                await virtualDrive.SetEnabledAsync(false);
                sycnStopped = true;
                StatusToSyncStopped();
            }
            else
            {
                await virtualDrive.SetEnabledAsync(true);
                sycnStopped = false;
                StatusToIdle();
            }
        }

        /// <summary>
        /// Start/stop synching evenet handler.
        /// </summary>
        public void HandleStatusChange(object sender, SynchEventArgs  synchEventArgs)
        {
            if (synchEventArgs.NewState == SynchronizationState.Synchronizing)
            {
                StatusToSynching();
            }
            else 
            {
                StatusToIdle();
            }
        }
    }
}
