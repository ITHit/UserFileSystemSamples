using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;

namespace VirtualDrive
{
    /// <summary>
    /// Monitors MS Office files renames in the user file system and sends changes to the remote storage.
    /// </summary>
    internal class MsOfficeDocsMonitor : Logger, IDisposable
    {
        /// <summary>
        /// User file system watcher.
        /// </summary>
        private readonly FileSystemWatcherQueued watcher = new FileSystemWatcherQueued();

        /// <summary>
        /// Engine.
        /// </summary>
        private readonly VirtualEngine engine;


        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemRootPath">User file system root path.</param>
        /// <param name="engine">Engine.</param>
        /// <param name="logger">Logger.</param>
        internal MsOfficeDocsMonitor(string userFileSystemRootPath, VirtualEngine engine, ILog log)
            : base("MS Office docs Monitor", log)
        {
            if(string.IsNullOrEmpty(userFileSystemRootPath))
            {
                throw new ArgumentNullException(nameof(userFileSystemRootPath));
            }
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));

            watcher.IncludeSubdirectories = true;
            watcher.Path = userFileSystemRootPath;
            //watcher.Filter = "*.*";

            // Some applications, such as Notpad++, remove the Offline attribute, 
            // Attributes filter is required to monitor the Changed event and convert the file back to the plceholder.
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Attributes;
            watcher.Error += Error;
            watcher.Created += CreatedAsync;
            watcher.Changed += ChangedAsync;
            watcher.Deleted += DeletedAsync;            
            watcher.Renamed += RenamedAsync;
        }

        /// <summary>
        /// Starts monitoring the user file system.
        /// </summary>
        public void Start()
        {
            watcher.EnableRaisingEvents = true;
            LogMessage("Started");
        }

        /// <summary>
        /// Stops monitoring the user file system.
        /// </summary>
        public void Stop() 
        {
            watcher.EnableRaisingEvents = false;
            LogMessage("Stopped");
        }


        /// <summary>
        /// Called when a file or folder is created in the user file system.
        /// </summary>
        private async void CreatedAsync(object sender, FileSystemEventArgs e)
        {
            LogMessage("Creating", e.FullPath);
        }

        /// <summary>
        /// Called when an item is updated in the user file system.
        /// </summary>
        private async void ChangedAsync(object sender, FileSystemEventArgs e)
        {
            LogMessage($"{e.ChangeType}", e.FullPath);

            string userFileSystemPath = e.FullPath;
            try
            {
                if (System.IO.File.Exists(userFileSystemPath)
                    && !MsOfficeHelper.AvoidMsOfficeSync(userFileSystemPath))
                {
                    if (!PlaceholderItem.IsPlaceholder(userFileSystemPath))
                    {
                        if (engine.CustomDataManager(userFileSystemPath).IsNew)
                        {
                            await engine.ClientNotifications(userFileSystemPath, this).CreateAsync();
                        }
                        else
                        {
                            LogMessage("Converting to placeholder", userFileSystemPath);
                            PlaceholderItem.ConvertToPlaceholder(userFileSystemPath, null, null, false);
                            await engine.ClientNotifications(userFileSystemPath, this).UpdateAsync();
                            await engine.CustomDataManager(userFileSystemPath).RefreshCustomColumnsAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string userFileSystemOldPath = null;
                if (e is RenamedEventArgs)
                {
                    userFileSystemOldPath = (e as RenamedEventArgs).OldFullPath;
                }
                LogError($"{e.ChangeType} failed", userFileSystemOldPath, userFileSystemPath, ex);
            }
        }

        /// <summary>
        /// Called when a file or folder is deleted in the user file system.
        /// </summary>
        private async void DeletedAsync(object sender, FileSystemEventArgs e)
        {
            LogMessage(e.ChangeType.ToString(), e.FullPath);
        }

        /// <summary>
        /// Called when a file or folder is renamed in the user file system.
        /// </summary>
        private async void RenamedAsync(object sender, RenamedEventArgs e)
        {
            // If the item was previously filtered by EngineWindows.FilterAsync(),
            // for example temp MS Office file was renamed SGE4274H -> file.xlsx,
            // we need to convert the file to a pleaceholder and upload it to the remote storage.

            ChangedAsync(sender, e);
        }

        private void Error(object sender, ErrorEventArgs e)
        {
            LogError(null, null, null, e.GetException());
        }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    watcher.Dispose();
                    LogMessage($"Disposed");
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ServerChangesMonitor()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
    }
}
