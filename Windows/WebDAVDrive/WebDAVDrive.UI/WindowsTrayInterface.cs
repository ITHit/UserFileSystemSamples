using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;

namespace WebDAVDrive.UI
{
    /// <summary>
    /// Represents tray application.
    /// </summary>
    public class WindowsTrayInterface : IDisposable
    {
        /// <summary>
        /// Stop this engine and exit this tray app menu.
        /// </summary>
        public readonly ToolStripItem MenuExit;

        /// <summary>
        /// Unmount menu.
        /// </summary>
        public readonly ToolStripItem MenuUnmount;

        /// <summary>
        /// Icon in the status bar notification area.
        /// </summary>
        private readonly NotifyIcon notifyIcon;

        /// <summary>
        /// App commands.
        /// </summary>
        private readonly Commands commands;

        /// <summary>
        /// Notify icon title.
        /// </summary>
        private readonly string title;

        /// <summary>
        /// Icon click handler delegete
        /// </summary>
        //public delegate void ItemClickHanler();

        /// <summary>
        /// Path to the icons folder.
        /// </summary>
        private readonly string iconsFolderPath;

        /// <summary>
        /// Context menu.
        /// </summary>
        private readonly ContextMenuStrip contextMenu;

        /// <summary>
        /// Start / Stop Engine menu.
        /// </summary>
        private readonly ToolStripItem menuStartStop;

        /// <summary>
        /// Show / Hide console menu.
        /// </summary>
        private readonly ToolStripItem menuConsole;

        private bool disposedValue;

        /// <summary>
        /// Starts a new tray application instance.
        /// </summary>s
        /// <param name="productName">Product name.</param>
        /// <param name="iconsFolderPath">Path to the icons folder.</param>
        /// <param name="commands">Engine commands.</param>
        /// <param name="engine">Engine instance. The tray app will start and stop this instance as well as will display its status.</param>
        /// <returns></returns>
        //public static async Task StartTrayInterfaceAsync(string productName, string webDavServerPath, string iconsFolderPath, Commands commands, EngineWindows engine)
        //{
        //    await Task.Run(async () =>
        //    {
        //        using (WindowsTrayInterface windowsTrayInterface = new WindowsTrayInterface(productName, webDavServerPath, iconsFolderPath, commands))
        //        {
        //            // Listen to engine notifications to change menu and icon states.
        //            engine.StateChanged += windowsTrayInterface.Engine_StateChanged;
        //            engine.SyncService.StateChanged += windowsTrayInterface.SyncService_StateChanged;

        //            Application.Run();
        //        }
        //    });
        //}

        /// <summary>
        /// Creates a tray application instance.
        /// </summary>
        /// <param name="title">Tray application title.</param>
        /// <param name="iconsFolderPath">Path to the icons folder.</param>
        /// <param name="commands">Engine commands.</param>
        public WindowsTrayInterface(string title, string webDavServerPath, string iconsFolderPath, Commands commands)
        {
            this.title = title;
            this.iconsFolderPath = iconsFolderPath ?? throw new ArgumentNullException(nameof(iconsFolderPath));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));

            notifyIcon = new NotifyIcon();

            notifyIcon.Visible = true;
            notifyIcon.Icon = new System.Drawing.Icon(Path.Combine(iconsFolderPath, "DrivePause.ico"));
            notifyIcon.Text = title;

            // Show/Hide console on app start. Required to hide the console in the release mode.
            WindowManager.SetConsoleWindowVisibility(WindowManager.ConsoleVisible);

            contextMenu = new ContextMenuStrip();

            // Add Start/Stop Engine icon.
            menuStartStop = new ToolStripMenuItem();
            menuStartStop.Text = Localization.Resources.StopSync;
            menuStartStop.Click += MenuStartStop_Click;
            contextMenu.Items.Add(menuStartStop);

            // Add Show/Hide console menu item.
            menuConsole = new ToolStripMenuItem();
            menuConsole.Text = WindowManager.ConsoleVisible ? Localization.Resources.HideLog : Localization.Resources.ShowLog;
            menuConsole.Click += MenuConsole_Click;
            contextMenu.Items.Add(menuConsole);

            // Add Open Folder menu item.
            ToolStripMenuItem menuOpenFolder = new ToolStripMenuItem();
            menuOpenFolder.Text = Localization.Resources.OpenFolder;
            menuOpenFolder.Click += async (s, e) => { await commands.OpenRootFolderAsync(); };
            contextMenu.Items.Add(menuOpenFolder);

            // Add open web browser menu item.
            ToolStripMenuItem menuOpenRemoteStorage = new ToolStripMenuItem();
            menuOpenRemoteStorage.Text = Localization.Resources.OpenRemoteStorage;
            menuOpenRemoteStorage.Click += async (s, e) => { await commands.OpenRemoteStorageAsync(); };
            contextMenu.Items.Add(menuOpenRemoteStorage);

            // Add menu separator.
            contextMenu.Items.Add(new ToolStripSeparator());

            // Add Exit menu item.
            //MenuExit = new ToolStripMenuItem();
            //MenuExit.Text = $"{Localization.Resources.Exit} {title}";
            //contextMenu.Items.Add(MenuExit);

            //// Add Unmount menu item.
            //MenuUnmount = new ToolStripMenuItem();
            //MenuUnmount.Text = $"{Localization.Resources.Unmount} {title}";
            //contextMenu.Items.Add(MenuUnmount);

            // Drive path, to distinguish tray applications for different drives.
            //ToolStripItem name = new ToolStripStatusLabel();
            //name.Text = title;
            //contextMenu.Items.Add(webDavServerPath);

            notifyIcon.ContextMenuStrip = contextMenu;
        }

        /// <summary>
        /// Start / Stop menu handler.
        /// </summary>
        private async void MenuStartStop_Click(object sender, EventArgs e)
        {
            await commands.StartStopEngineAsync();
        }

        /// <summary>
        /// Show / Hide console.
        /// </summary>
        private void MenuConsole_Click(object sender, EventArgs e)
        {
            WindowManager.SetConsoleWindowVisibility(!WindowManager.ConsoleVisible);
            menuConsole.Text = WindowManager.ConsoleVisible ? Localization.Resources.HideLog : Localization.Resources.ShowLog;
        }

        /// <summary>
        /// Updates title and image of Start/Stop menu item.
        /// </summary>
        /// <param name="state">Engine state</param>
        private void UpdateMenuStartStop(EngineState state)
        {
            switch (state)
            {
                case EngineState.Running:
                    menuStartStop.Text = Localization.Resources.StopSync;
                    notifyIcon.Text = $"{title}\n{Localization.Resources.Idle}";
                    notifyIcon.Icon = new System.Drawing.Icon(Path.Combine(iconsFolderPath, "Drive.ico"));
                    break;
                case EngineState.Stopped:
                    menuStartStop.Text = Localization.Resources.StartSync;
                    notifyIcon.Text = $"{title}\n{Localization.Resources.StatusSyncStopped}";
                    notifyIcon.Icon = new System.Drawing.Icon(Path.Combine(iconsFolderPath, "DrivePause.ico"));
                    break;
            }
        }

        /// <summary>
        /// Fired on Engine status change.
        /// </summary>
        /// <param name="engine">Engine</param>
        /// <param name="e">Contains new and old Engine state.</param>
        public void Engine_StateChanged(Engine engine, EngineWindows.StateChangeEventArgs e)
        {
            if (!disposedValue)
            {
                if (contextMenu.IsHandleCreated)
                {
                    contextMenu.Invoke(() =>
                    {
                        UpdateMenuStartStop(e.NewState);
                    });
                }
                else
                {
                    UpdateMenuStartStop(e.NewState);
                }
            }
        }

        /// <summary>
        /// Fired on sync service status change.
        /// </summary>
        /// <param name="sender">Sync service.</param>
        /// <param name="e">Contains new and old sync service state.</param>
        public void SyncService_StateChanged(object sender, SynchEventArgs e)
        {
            if (!disposedValue)
            {
                switch (e.NewState)
                {
                    case SynchronizationState.Synchronizing:
                        notifyIcon.Text = $"{title}\n{Localization.Resources.StatusSync}";
                        notifyIcon.Icon = new System.Drawing.Icon(Path.Combine(iconsFolderPath, "DriveSync.ico"));
                        break;

                    case SynchronizationState.Idle:
                        notifyIcon.Text = $"{title}\n{Localization.Resources.Idle}";
                        notifyIcon.Icon = new System.Drawing.Icon(Path.Combine(iconsFolderPath, "Drive.ico"));
                        break;
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    notifyIcon?.Dispose();
                }

                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~WindowsTrayInterface()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
