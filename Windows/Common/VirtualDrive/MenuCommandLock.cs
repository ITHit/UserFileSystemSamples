using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ITHit.FileSystem.Windows;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    
    /// <summary>
    /// Implements lock and unlock context menu command displayed in a file manager.
    /// The menu is shown only if all selected items are locked by this client or all items are unlocked. 
    /// Otherwise the menu is hidden.
    /// </summary>
    public class MenuCommandLock : IMenuCommandWindows
    {
        private readonly EngineWindows engine;
        private readonly ILogger logger;

        private const string lockCommandIcon = @"Images\Locked.ico";
        private const string unlockCommandIcon = @"Images\Unlocked.ico";

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public MenuCommandLock(EngineWindows engine, ILogger logger)
        {
            this.engine = engine;
            this.logger = logger.CreateLogger("Lock Menu Command");
        }

        /// <inheritdoc/>
        public async Task<string> GetTitleAsync(IEnumerable<string> filesPath)
        {
            bool isLocked = await IsLockedAsync(filesPath) == true;
            return isLocked ? "Unlock" : "Lock";
        }

        /// <inheritdoc/>
        public async Task<string> GetIconAsync(IEnumerable<string> filesPath)
        {
            string iconName = await IsLockedAsync(filesPath) == false ? lockCommandIcon : unlockCommandIcon;
            string iconPath = Path.Combine(Path.GetDirectoryName(typeof(MenuCommandLock).Assembly.Location), iconName);
            return iconPath;
        }

        /// <inheritdoc/>
        public async Task<MenuState> GetStateAsync(IEnumerable<string> filesPath)
        {
            bool? isLocked = await IsLockedAsync(filesPath);
            return isLocked.HasValue ? MenuState.Enabled : MenuState.Hidden;
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(IEnumerable<string> filesPath)
        {
            // If you need a remote storage ID for each item use the following code:
            //foreach (string userFileSystemPath in filesPath)
            //{
            //    if(engine.Placeholders.TryGetItem(userFileSystemPath, out PlaceholderItem placeholder))
            //    {
            //        byte[] remoteStorageId = placeholder.GetRemoteStorageItemId();
            //    }
            //}

            bool isLocked = await IsLockedAsync(filesPath) == true;
            foreach (string userFileSystemPath in filesPath)
            {
                try
                {
                    IClientNotifications clientNotifications = engine.ClientNotifications(userFileSystemPath, logger);
                    if (isLocked)
                        await clientNotifications.UnlockAsync();
                    else
                        await clientNotifications.LockAsync();
                }
                catch (Exception ex)
                {
                    string actionName = isLocked ? "Unlock" : "Lock";
                    logger.LogError($"Failed to {actionName} item", userFileSystemPath, null, ex);
                }
            }
        }

        /// <inheritdoc/>
        public async Task<string> GetToolTipAsync(IEnumerable<string> filesPath)
        {
            bool isLocked = await IsLockedAsync(filesPath) == true;
            return isLocked ? "Unlock item(s)" : "Lock item(s)";
        }

        /// <summary>
        /// Returns files lock status.
        /// </summary>
        /// <remarks>
        /// True - if all items are locked. False - if all items are unlocked. null - if some items are locked, others unlocked.
        /// </remarks>
        private async Task<bool?> IsLockedAsync(IEnumerable<string> filesPath, CancellationToken cancellationToken = default)
        {
            bool? allLocked = null;
            foreach (string userFileSystemPath in filesPath)
            {
                try
                {
                    IClientNotifications clientNotifications = engine.ClientNotifications(userFileSystemPath, logger);
                    LockMode lockMode = await clientNotifications.GetLockModeAsync(cancellationToken);

                    bool isLocked = lockMode != LockMode.None;

                    if(allLocked.HasValue && (allLocked != isLocked))
                    {
                        return null;
                    }

                    allLocked = isLocked;
                }
                catch (Exception ex)
                {
                    logger.LogError("Failed to get lock state", userFileSystemPath, null, ex);
                }
            }

            return allLocked;
        }
    }
    
}
