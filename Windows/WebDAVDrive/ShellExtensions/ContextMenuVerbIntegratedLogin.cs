using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Windows.ShellExtension;
using WebDAVDrive.Services;

namespace WebDAVDrive.ShellExtensions
{
    /// <summary>
    /// Implements Windows Explorer Login context menu, displayed on a root node.
    /// </summary>
    [ComVisible(true)]
    [ProgId("WebDAVDrive.ContextMenuVerbLogin")]
    [Guid("A0DFD8FF-D8F8-4670-A11E-2BB386D16ECA")]
    [MenuCommand(typeof(MenuCommandLogin))]
    public class ContextMenuVerbIntegratedLogin : CloudFilesContextMenuVerbIntegratedBase
    {
        public ContextMenuVerbIntegratedLogin() : base(ServiceProvider.GetService<IDrivesService>().GetEngineWindowsDictionary())
        {
        }
    }

    /// <summary>
    /// Implements Login menu command displayed in a file manager.
    /// The menu is shown on root items.
    /// </summary>
    public class MenuCommandLogin: IMenuCommandWindows
    {
        private readonly VirtualEngine engine;
        private readonly ILogger logger;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public MenuCommandLogin(VirtualEngine engine, ILogger logger)
        {
            this.engine = engine;
            this.logger = logger.CreateLogger("Login Menu Command");
        }

        /// <inheritdoc/>
        public async Task<string> GetTitleAsync(IEnumerable<string> filesPath)
        {
            bool authenticated = await engine.IsAuthenticatedAsync(null, new CancellationToken());
            return authenticated ? "Logout..." : "Login...";
        }

        /// <inheritdoc/>
        public async Task<string> GetIconAsync(IEnumerable<string> filesPath)
        {
            bool authenticated = await engine.IsAuthenticatedAsync(null, new CancellationToken());
            string iconPath = string.Empty;
            if (authenticated)
            {
                iconPath = ServiceProvider.IsDarkTheme ? @"Images\LogoutWhite.png" : @"Images\Logout.png";
            }
            else
            {
                iconPath = ServiceProvider.IsDarkTheme ? @"Images\LoginWhite.png" : @"Images\Login.png";
            }
            return Path.Combine(Path.GetDirectoryName(typeof(MenuCommandLogin).Assembly.Location), iconPath);
        }

        /// <inheritdoc/>
        public async Task<MenuState> GetStateAsync(IEnumerable<string> filesPath)
        {
            // Show menu on root folder, in all cases except case when the engine does not require creds ("authenticated anonymous")
            if (filesPath.Count() == 1)
            {
                string userFileSystemPath = filesPath.First().TrimEnd('\\');
                if (engine.Path.TrimEnd('\\').Equals(userFileSystemPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    bool authenticated = await engine.IsAuthenticatedAsync(null, new CancellationToken());
                    return (authenticated && !engine.DavClientCredentialsSet) ? MenuState.Hidden : MenuState.Enabled;
                }
            }
            return MenuState.Hidden;
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(IEnumerable<string> filesPath, IEnumerable<byte[]> remoteStorageItemIds = null, CancellationToken cancellationToken = default)
        {
            // Must create new thread to avoid deadlock.
            bool authenticated = await engine.IsAuthenticatedAsync(null, new CancellationToken());
            if (authenticated)
            {
                await Task.Run(async () => await engine.LogoutAsync());
            }
            else
            {
                await Task.Run(async () => await engine.StartAsync());
            }
        }

        /// <inheritdoc/>
        public async Task<string> GetToolTipAsync(IEnumerable<string> filesPath)
        {
            bool authenticated = await engine.IsAuthenticatedAsync(null, new CancellationToken());
            return authenticated ? "Logout from engine" : "Login to engine";
        }
    }
}
