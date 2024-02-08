using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows;


namespace WebDAVDrive
{
    /// <summary>
    /// Implements compare menu command displayed in a file manager.
    /// The menu is shown only if the item is in conflict state and a single item is selected. 
    /// Otherwise the menu is hidden.
    /// </summary>
    public class MenuCommandCompare : IMenuCommandWindows
    {
        private readonly VirtualEngineBase engine;
        private readonly ILogger logger;

        //private const string lockCommandIcon = @"Images\Locked.ico";
        //private const string unlockCommandIcon = @"Images\Unlocked.ico";

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
            //string iconPath = Path.Combine(Path.GetDirectoryName(typeof(MenuCommandLock).Assembly.Location), iconName);
            return null;
        }

        /// <inheritdoc/>
        public async Task<MenuState> GetStateAsync(IEnumerable<string> filesPath)
        {
            // Show menu only if a single item is selected and the item is in conflict state.
            if (filesPath.Count() == 1)
            {
                string userFileSystemPath = filesPath.First();
                if (engine.Placeholders.TryGetItem(userFileSystemPath, out PlaceholderItem placeholder))
                {
                    if (placeholder.TryGetErrorStatus(out bool errorStatus) && errorStatus)
                    {
                        return MenuState.Enabled;
                    }
                }
            }
            return MenuState.Hidden;
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(IEnumerable<string> filesPath, IEnumerable<byte[]> remoteStorageItemIds = null)
        {
            string userFileSystemPath = filesPath.First();



        }

        /// <inheritdoc/>
        public async Task<string> GetToolTipAsync(IEnumerable<string> filesPath)
        {
            return "Compare client and server files";
        }
    }
}
