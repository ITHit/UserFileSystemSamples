using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Samples.Common.Syncronyzation;
using static ITHit.FileSystem.Samples.Common.Syncronyzation.FullSyncService;

namespace WebDAVDrive.UI
{
    /// <summary>
    /// Represents tray application.
    /// </summary>
    public class WindowsTrayInterface
    {
        /// <summary>
        /// Create new tray icon.
        /// </summary>
        /// <param name="productName">Product name.</param>
        /// <param name="syncService">Sync service</param>
        /// <param name="exitEvent">ManualResetEvent, used to stop application</param>
        /// <returns></returns>
        public static Thread CreateTrayInterface(string productName, FullSyncService syncService, ManualResetEvent exitEvent) 
        {
            // Start tray application.
            Thread thread = new Thread(() => {
                WindowsTrayInterface windowsTrayInterface = new WindowsTrayInterface($"{productName}", syncService);
                syncService.syncEvent += windowsTrayInterface.HandleStatusChange;
                Application.Run();
                exitEvent.Set();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.IsBackground = true;
            return thread;
        }

        /// <summary>
        /// Changes button status to Idle
        /// </summary>
        private void StatusToIdle() 
        {
            notifyIcon.Text = Title + $"\n{Localization.Resources.Idle}";
            notifyIcon.ContextMenuStrip.Items[0].Text = Localization.Resources.StopSync;
        }

        /// <summary>
        /// Changes button status to Synching
        /// </summary>
        private void StatusToSynching() 
        {
            notifyIcon.Text = Title + $"\n{Localization.Resources.StatusSync}";
            notifyIcon.ContextMenuStrip.Items[0].Text = Localization.Resources.StopSync;
        }

        private void StatusToSyncStopped() 
        {
            notifyIcon.Text = Title + $"\n{Localization.Resources.StopSync}";
            notifyIcon.ContextMenuStrip.Items[0].Text = Localization.Resources.StartSync;
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
        /// Notify icon title
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
        public WindowsTrayInterface(string title, FullSyncService syncService) 
        {
            Title = title;
            notifyIcon = new NotifyIcon();

            notifyIcon.Visible = true;
            notifyIcon.Icon = new System.Drawing.Icon("Images\\Drive.ico");
            notifyIcon.Text = title;

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add(Localization.Resources.StopSync, null, (s, e) => { StartStopSync(syncService); });
#if !DEBUG
            // Hide console on app start.
            Visible = false;
            SetConsoleWindowVisibility(false);
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

            notifyIcon.MouseClick += (object sender, MouseEventArgs e) => 
            {
                //MessageBox.Show("Clicked");
            };
        }

        /// <summary>
        /// Defines StartStop sync button in tray app.
        /// </summary>
        private static bool sycnStopped = false;

        /// <summary>
        /// This method handles StartStop Sycn button in tray menu.
        /// </summary>
        private async void StartStopSync(FullSyncService syncService)
        {
            if (!sycnStopped)
            {
                await syncService.StopAsync();
                sycnStopped = true;
                StatusToSyncStopped();
            }
            else
            {
                await syncService.StartAsync();
                sycnStopped = false;
                StatusToIdle();
            }
        }

        /// <summary>
        /// Start/stop synching evenet handler.
        /// </summary>
        public void HandleStatusChange(object sender, SynchEventArgs  synchEventArgs)
        {
            if (synchEventArgs.state == SynchronizationState.Started)
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
