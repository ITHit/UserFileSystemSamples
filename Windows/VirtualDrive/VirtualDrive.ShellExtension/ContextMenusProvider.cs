using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VirtualDrive.ShellExtension.Interop;
using VirtualDrive.Rpc;
using VirtualDrive.Rpc.Generated;
using GrpcDotNetNamedPipes;

namespace VirtualDrive.ShellExtension
{
    /// <summary>
    /// Implements context menu logic.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    [ComVisible(true)]
    [ProgId("VirtualDrive.ContextMenusProvider"), Guid(ContextMenusClass)]
    public class ContextMenusProvider : ContextMenusProviderBase
    {
        public const string ContextMenusClass = "A22EBD03-343E-433C-98DF-372C6B3A1538";
        public static readonly Guid ContextMenusClassGuid = Guid.Parse(ContextMenusClass);

        private bool actionLock = true;

        /// <summary>
        /// Returns menu title depends on response from server.
        /// </summary>
        public override async Task<string> GetMenuTitleAsync(IEnumerable<string> filesPath)
        {
            actionLock = await GetLockStatusAsync(filesPath);

            bool multyFiles = filesPath.Count() > 1;

            string action = actionLock ? "Unlock " : "Lock ";
            string objects = multyFiles ? "files" : "file";

            return action + objects;
        }

        /// <summary>
        /// Call server to change files lock status.
        /// </summary>
        public override async Task InvokeMenuCommandAsync(IEnumerable<string> filesPath)
        {
            if (!filesPath.Any())
                throw new NotImplementedException();

            GrpcClient grpcClient = new GrpcClient();

            var request = new ItemsStatusList();
            
            foreach (string path in filesPath)
            {
                request.FilesStatus.Add(path, !actionLock);
            }

            await grpcClient.RpcClient.SetLockStatusAsync(request);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Returns display or hiden menu item depends on files lock states.
        /// </summary>
        public override async Task<EXPCMDSTATE> GetMenuStateAsync(IEnumerable<string> filesPath)
        {
            try
            {
                actionLock = await GetLockStatusAsync(filesPath);

                return EXPCMDSTATE.ECS_ENABLED;
            }
            catch (NotImplementedException)
            {
                return EXPCMDSTATE.ECS_HIDDEN;
            }
        }

        /// <summary>
        /// Calls server to get files lock state and check if they are same
        /// </summary>
        /// <param name="filesPath"></param>
        /// <returns></returns>
        private async Task<bool> GetLockStatusAsync(IEnumerable<string> filesPath)
        {
            if (!filesPath.Any())
                throw new NotImplementedException();

            GrpcClient grpcClient = new GrpcClient();

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
