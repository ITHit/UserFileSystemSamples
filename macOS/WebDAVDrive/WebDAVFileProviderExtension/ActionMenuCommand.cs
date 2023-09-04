using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ITHit.FileSystem;

namespace WebDAVFileProviderExtension
{
    [Guid("28d0e0cb-5df7-432c-a06f-297c06b5ec5d")]
    public class ActionMenuCommand : IMenuCommand
    {
        private readonly VirtualEngine engine;
        private readonly ILogger logger;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public ActionMenuCommand(VirtualEngine engine, ILogger logger)
        {
            this.engine = engine;
            this.logger = logger.CreateLogger("Simple Action Command");
        }
      
        public async Task InvokeAsync(IEnumerable<string> filesPath, IEnumerable<byte[]> remoteStorageItemIds)
        {
            logger.LogMessage($"Action Menu items: {string.Join(",", remoteStorageItemIds.Select(p => Encoding.UTF8.GetString(p)))}");
        }
    }
}
