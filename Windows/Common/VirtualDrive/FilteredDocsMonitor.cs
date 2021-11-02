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

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Monitors MS Office amd AutoCAD file renames and updates in the user file system and sends changes to the remote storage. 
    /// Also monitors Notepad++ offline attribute removal.
    /// </summary>
    public class FilteredDocsMonitor : Logger, IDisposable
    {
        /// <summary>
        /// User file system watcher.
        /// </summary>
        private readonly FileSystemWatcherQueued watcher = new FileSystemWatcherQueued();

        /// <summary>
        /// Engine.
        /// </summary>
        private readonly VirtualEngineBase engine;


        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="userFileSystemRootPath">User file system root path.</param>
        /// <param name="engine">Engine.</param>
        /// <param name="logger">Logger.</param>
        internal FilteredDocsMonitor(string userFileSystemRootPath, VirtualEngineBase engine, ILog log)
            : base("Filtered Docs Monitor", log)
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
            LogMessage("Started", watcher.Path);
        }

        /// <summary>
        /// Stops monitoring the user file system.
        /// </summary>
        public void Stop() 
        {
            watcher.EnableRaisingEvents = false;
            LogMessage("Stopped", watcher.Path);
        }


        /// <summary>
        /// Called when a file or folder is created in the user file system.
        /// </summary>
        private async void CreatedAsync(object sender, FileSystemEventArgs e)
        {
            LogMessage(e.ChangeType.ToString(), e.FullPath);
        }

        /// <summary>
        /// Called when a file or folder is deleted in the user file system.
        /// </summary>
        private async void DeletedAsync(object sender, FileSystemEventArgs e)
        {
            LogMessage(e.ChangeType.ToString(), e.FullPath);
        }

        /// <summary>
        /// Called when an item is updated in the user file system.
        /// </summary>
        private async void ChangedAsync(object sender, FileSystemEventArgs e)
        {
            LogMessage($"{e.ChangeType}", e.FullPath);
            await CreateOrUpdateAsync(sender, e);
        }

        /// <summary>
        /// Called when a file or folder is renamed in the user file system.
        /// </summary>
        private async void RenamedAsync(object sender, RenamedEventArgs e)
        {
            // If the item was previously filtered by EngineWindows.FilterAsync(),
            // for example temp MS Office file was renamed SGE4274H -> file.xlsx,
            // we need to convert the file to a pleaceholder and upload it to the remote storage.

            LogMessage($"{e.ChangeType}", e.OldFullPath, e.FullPath);
            await CreateOrUpdateAsync(sender, e);
        }

        /// <summary>
        /// Creates the item in the remote storate if the item is new. 
        /// Updates the item in the remote storage if the item in not new.
        /// </summary>
        private async Task CreateOrUpdateAsync(object sender, FileSystemEventArgs e)
        {
            string userFileSystemPath = e.FullPath;
            string userFileSystemOldPath = (e is RenamedEventArgs) ? (e as RenamedEventArgs).OldFullPath : null;
            try
            {
                await ClientToServerSync.CreateOrUpdateAsync(userFileSystemPath, engine, this);
            }
            catch (FileNotFoundException ex)
            {
                // Some temp file was renamed or deleted.
                LogMessage($"{e.ChangeType} failed", userFileSystemOldPath, userFileSystemPath);
            }
            catch (Exception ex)
            {

                LogError($"{e.ChangeType} failed", userFileSystemOldPath, userFileSystemPath, ex);
            }
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
