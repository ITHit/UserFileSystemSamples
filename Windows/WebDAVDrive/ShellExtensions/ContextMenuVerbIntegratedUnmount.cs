using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Windows.ShellExtension;
using WebDAVDrive.Services;

namespace WebDAVDrive.ShellExtensions
{
    /// <summary>
    /// Implements Windows Explorer Unmount context menu, displayed on a root node.
    /// </summary>
    [ComVisible(true)]
    [ProgId("WebDAVDrive.ContextMenuVerbUnmount")]
    [Guid("9FC5E094-5F3B-4417-995E-68ABF987CF66")]
    [MenuCommand(typeof(MenuCommandUnmount))]
    public class ContextMenuVerbIntegratedUnmount : CloudFilesContextMenuVerbIntegratedBase
    {
        public ContextMenuVerbIntegratedUnmount() : base(ServiceProvider.GetService<IDrivesService>().GetEngineWindowsDictionary())
        {
        }
    }

    /// <summary>
    /// Implements Unmount menu command displayed in a file manager.
    /// The menu is shown on root items.
    /// </summary>
    public class MenuCommandUnmount : IMenuCommandWindows
    {
        private readonly VirtualEngine engine;
        private readonly IDrivesService domainsService;
        private readonly ILogger logger;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public MenuCommandUnmount(VirtualEngine engine, ILogger logger)
        {
            domainsService = ServiceProvider.GetService<IDrivesService>();
            this.engine = engine;
            this.logger = logger.CreateLogger("Unmount Menu Command");
        }

        /// <inheritdoc/>
        public async Task<string> GetTitleAsync(IEnumerable<string> filesPath)
        {
            return "Unmount...";
        }

        /// <inheritdoc/>
        public async Task<string> GetIconAsync(IEnumerable<string> filesPath)
        {
            string iconPath = ServiceProvider.IsDarkTheme ? @"Images\UnmountWhite.png" : @"Images\Unmount.png";
            return Path.Combine(Path.GetDirectoryName(typeof(MenuCommandUnmount).Assembly.Location), iconPath);
        }

        /// <inheritdoc/>
        public async Task<MenuState> GetStateAsync(IEnumerable<string> filesPath)
        {
            // Show menu only if a single item is selected and the item is in conflict state.
            if (filesPath.Count() == 1)
            {
                string userFileSystemPath = filesPath.First().TrimEnd('\\');
                if (engine.Path.TrimEnd('\\').Equals(userFileSystemPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    return MenuState.Enabled;
                }
            }
            return MenuState.Hidden;
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(IEnumerable<string> filesPath, IEnumerable<byte[]> remoteStorageItemIds = null, CancellationToken cancellationToken = default)
        {
            await engine.Commands.StopEngineAsync();
            await domainsService.UnMountAsync(engine.InstanceId);
        }

        /// <inheritdoc/>
        public async Task<string> GetToolTipAsync(IEnumerable<string> filesPath)
        {
            return "Unmount this drive";
        }
    }
}
