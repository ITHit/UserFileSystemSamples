using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;

using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem;
using ITHit.FileSystem.Windows.ShellExtension;

namespace VirtualDrive.ShellExtension
{
    
    /// <summary>
    /// Implements Windows Explorer context menu. Runs in one process with Engine.
    /// </summary>
    [ComVisible(true)]
    [ProgId("VirtualDrive.ContextMenuVerb")]
    [Guid("9C923BF3-3A4B-487B-AB4E-B4CF87FD1C25")]
    [MenuCommand(typeof(MenuCommandLock))]
    public class ContextMenuVerbIntegrated : CloudFilesContextMenuVerbIntegratedBase
    {
        public ContextMenuVerbIntegrated() : base(Program.Engine)
        {
        }
    }
    


    
    /// <summary>
    /// Implements lock and unlock context menu command displayed in a file manager.
    /// The menu is shown only if all selected items are locked by this client or all items are unlocked. 
    /// Otherwise the menu is hidden.
    /// </summary>
    public class MenuCommandLock : IMenuCommandWindows
    {
        private readonly VirtualEngineBase engine;
        private readonly ILogger logger;

        private const string lockCommandIcon = @"Images\Locked.ico";
        private const string unlockCommandIcon = @"Images\Unlocked.ico";

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public MenuCommandLock(VirtualEngineBase engine, ILogger logger)
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
            // This sample can not lock folders.
            // Hide menu if any folders are selected.
            foreach (string userFileSystemPath in filesPath)
            {
                FileAttributes attr = File.GetAttributes(userFileSystemPath);
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                    return MenuState.Hidden;
            }

            bool? isLocked = await IsLockedAsync(filesPath);
            return isLocked.HasValue ? MenuState.Enabled : MenuState.Hidden;
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(IEnumerable<string> filesPath, IEnumerable<byte[]> remoteStorageItemIds = null, CancellationToken cancellationToken = default)
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
        /// True - if all items are locked by this user. 
        /// False - if all items are unlocked. 
        /// null - if some items are locked, others unlocked or if any item is locked by another user.
        /// </remarks>
        private async Task<bool?> IsLockedAsync(IEnumerable<string> filesPath, CancellationToken cancellationToken = default)
        {
            bool? allLocked = null;
            foreach (string userFileSystemPath in filesPath)
            {
                try
                {
                    bool isLocked = false;
                    if (engine.Placeholders.TryGetItem(userFileSystemPath, out PlaceholderItem placeholder))
                    {
                        if (placeholder.Properties.TryGetActiveLockInfo(out ServerLockInfo lockInfo))
                        {
                            // Detect if locked by this user.
                            if (!engine.IsCurrentUser(lockInfo.Owner))
                            {
                                // Typically we can not unlock items locked by other users.
                                // We must hide or disable menu in this case.
                                return null;
                            }
                            isLocked = true;
                        }
                    }

                    if (allLocked.HasValue && (allLocked != isLocked))
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
