using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ITHit.FileSystem.Samples.Common.Windows.Rpc;
using ITHit.FileSystem.Samples.Common.Windows.Rpc.Generated;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Interop;

namespace VirtualDrive.ShellExtension
{
    /// <summary>
    /// Implements Windows Explorer context menu.
    /// </summary>
    [ComVisible(true)]
    [ProgId("VirtualDrive.ContextMenusProvider"), Guid(ContextMenusClass)]
    public class ContextMenusProvider : ContextMenusProviderBase
    {
        public const string ContextMenusClass = "9C923BF3-3A4B-487B-AB4E-B4CF87FD1C25";
        public static readonly Guid ContextMenusClassGuid = Guid.Parse(ContextMenusClass);

        public const string LockCommandIcon = "Locked.ico";
        public const string UnlockCommandIcon = "Unlocked.ico";

        /// <summary>
        /// Selected items status. True - if all items are locked. False - if all items are unlocked.
        /// </summary>
        private bool isLocked = true;

        /// <summary>
        /// Gets menu title.
        /// </summary>
        /// <param name="filesPath">List of selected items.</param>
        /// <returns>Menu title string.</returns>
        public override async Task<string> GetMenuTitleAsync(IEnumerable<string> filesPath)
        {
            isLocked = await GetLockStatusAsync(filesPath);

            bool multyFiles = filesPath.Count() > 1;

            string action = isLocked ? "Unlock " : "Lock ";
            string objects = multyFiles ? "files" : "file";

            return action + objects;
        }

        /// <summary>
        /// Sets files lock status.
        /// </summary>
        /// <param name="filesPath">List of selected items.</param>
        public override async Task InvokeMenuCommandAsync(IEnumerable<string> filesPath)
        {
            if (!filesPath.Any())
                throw new NotImplementedException();

            GrpcClient grpcClient = new GrpcClient(ShellExtensionConfiguration.AppSettings.RpcCommunicationChannelName);

            ItemsStatusList itemStatusList = new ItemsStatusList();

            foreach (string path in filesPath)
            {
                itemStatusList.FilesStatus.Add(path, !isLocked);
            }

            await grpcClient.RpcClient.SetLockStatusAsync(itemStatusList);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Gets menu state - visible or hidden, depending on the selected items lock state.
        /// </summary>
        /// <param name="filesPath">List of selected items.</param>
        /// <returns>Item state.</returns>
        public override async Task<EXPCMDSTATE> GetMenuStateAsync(IEnumerable<string> filesPath)
        {
            try
            {
                isLocked = await GetLockStatusAsync(filesPath);

                return EXPCMDSTATE.ECS_ENABLED;
            }
            catch (NotImplementedException)
            {
                return EXPCMDSTATE.ECS_HIDDEN;
            }
        }

        /// <summary>
        /// Returns path to icon file or resource.
        /// </summary>
        /// <param name="filesPath">List of selected items.</param>
        /// <returns>Path to icon file or resource.</returns>
        public override async Task<string> GetIconAsync(IEnumerable<string> filesPath)
        {
            string iconName = isLocked ? UnlockCommandIcon : LockCommandIcon;
            string iconPath = Path.Combine(Path.GetDirectoryName(typeof(ContextMenusProvider).Assembly.Location), iconName);

            return iconPath;
        }

        /// <summary>
        /// Calls main application to get files lock state and checks if all items have the same state.
        /// </summary>
        /// <param name="filesPath">List of items to get state for.</param>
        /// <returns>True if all items has locked state. False if all items has unlocked state.</returns>
        private async Task<bool> GetLockStatusAsync(IEnumerable<string> filesPath)
        {
            if (!filesPath.Any())
                throw new NotImplementedException();

            GrpcClient grpcClient = new GrpcClient(ShellExtensionConfiguration.AppSettings.RpcCommunicationChannelName);

            var request = new ItemsPathList();
            request.Files.AddRange(filesPath);

            ItemsStatusList filesStatus = await grpcClient.RpcClient.GetLockStatusAsync(request);

            if (filesStatus.FilesStatus.Count() != filesPath.Count())
                throw new NotImplementedException();

            bool allHasSameValue = filesStatus.FilesStatus.Values.Distinct().Count() == 1;
            if (!allHasSameValue)
                throw new NotImplementedException();

            bool lockStatus = filesStatus.FilesStatus.Values.First();
            return lockStatus;
        }
    }
}
