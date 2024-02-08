using System;
using ITHit.FileSystem;
using ITHit.WebDAV.Client;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ITHit.FileSystem.Mac;

namespace WebDAVFileProviderExtension
{
    [Guid("28d0e0cb-5df7-432c-a06f-297c06b5ec4d")]
    public class UnLockMenuCommand : IMenuCommand
    {
        private readonly VirtualEngine engine;
        private readonly ILogger logger;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public UnLockMenuCommand(VirtualEngine engine, ILogger logger)
        {
            this.engine = engine;
            this.logger = logger.CreateLogger(nameof(UnLockMenuCommand));
        }

        public async Task InvokeAsync(IEnumerable<string> filesPath, IEnumerable<byte[]> remoteStorageItemIds)
        {
            logger.LogMessage($"{nameof(UnLockMenuCommand)}.{nameof(InvokeAsync)}()", string.Join(",", remoteStorageItemIds.Select(p => Encoding.UTF8.GetString(p))));

            foreach (byte[] remoteStorageItemId in remoteStorageItemIds)
            {
                IFileSystemItem fileSystemItem = await engine.GetFileSystemItemAsync(remoteStorageItemId, FileSystemItemType.File, null);
                if (fileSystemItem != null)
                {
                    await ((ILock)fileSystemItem).UnlockAsync(null, default);
                }                
            }
        }
    }
}

