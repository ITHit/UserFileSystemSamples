using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;

using WebDAVDrive.Dialogs;
using WinUIEx;

namespace WebDAVDrive
{
    /// <summary>
    /// Implements compare menu command displayed in a file manager.
    /// </summary>
    public class MenuCommandCompare : IMenuCommandWindows
    {
        private readonly VirtualEngineBase engine;
        private readonly ILogger logger;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public MenuCommandCompare(VirtualEngineBase engine, ILogger logger)
        {
            this.engine = engine;
            this.logger = logger.CreateLogger("Compare Menu Command");
        }

        /// <inheritdoc/>
        public async Task<string> GetTitleAsync(IEnumerable<string> filesPath)
        {
            return "Compare...";
        }

        /// <inheritdoc/>
        public async Task<string> GetIconAsync(IEnumerable<string> filesPath)
        {
            return Path.Combine(Path.GetDirectoryName(typeof(MenuCommandCompare).Assembly.Location), @"Images\Compare.ico");
        }

        /// <inheritdoc/>
        public async Task<MenuState> GetStateAsync(IEnumerable<string> filesPath)
        {
            // Show menu only if a single item is selected and the item is in conflict state.
            if (filesPath.Count() == 1)
            {
                string userFileSystemPath = filesPath.First();
                FileAttributes atts = File.GetAttributes(userFileSystemPath);

                // Enable menu for online files.
                if (!atts.HasFlag(FileAttributes.Offline) && !atts.HasFlag(FileAttributes.Directory))
                {
                    return MenuState.Enabled;
                }
                /*
                // The menu is shown only if the item is in conflict state and a single item is selected. 
                // Otherwise the menu is hidden.
                if (engine.Placeholders.TryGetItem(userFileSystemPath, out PlaceholderItem placeholder))
                {
                    
                    if (placeholder.TryGetErrorStatus(out bool errorStatus) && errorStatus)
                    {
                        return MenuState.Enabled;
                    }
                }
                */
            }
            return MenuState.Disabled;
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(IEnumerable<string> filesPath, IEnumerable<byte[]> remoteStorageItemIds = null, CancellationToken cancellationToken = default)
        {
            string userFileSystemPath = filesPath.First();

            //if (engine.Placeholders.TryGetItem(userFileSystemPath, out PlaceholderItem placeholder))
            //{
            //    OperationResult res = await (placeholder as PlaceholderFile).TryShadowDownloadAsync(default, logger, cancellationToken);

            //    ITHit.FileSystem.Windows.AppHelper.Utilities.TryCompare(placeholder.Path, res.ShadowFilePath);
            //}

            _ = ServiceProvider.DispatcherQueue.TryEnqueue(() => new Compare(userFileSystemPath, engine as VirtualEngine).Show());
        }

        /// <inheritdoc/>
        public async Task<string> GetToolTipAsync(IEnumerable<string> filesPath)
        {
            return "Compare local and remote files";
        }
    }
}
