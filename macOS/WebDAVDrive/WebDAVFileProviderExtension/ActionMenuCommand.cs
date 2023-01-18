using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

        public Task<string> GetIconAsync(IEnumerable<string> filesPath)
        {
            throw new NotImplementedException();
        }

        public Task<MenuState> GetStateAsync(IEnumerable<string> filesPath)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetTitleAsync(IEnumerable<string> filesPath)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetToolTipAsync(IEnumerable<string> filesPath)
        {
            throw new NotImplementedException();
        }

        public async Task InvokeAsync(IEnumerable<string> filesPath)
        {
            logger.LogMessage($"Action Menu items: {string.Join(",", filesPath)}");
        }
    }
}
