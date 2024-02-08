using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Foundation;
using ITHit.FileSystem;
using ITHit.FileSystem.Mac;
using ITHit.WebDAV.Client;

namespace WebDAVFileProviderExtension
{
    [Guid("18d0e0cb-5df7-432c-a06f-297c06b5ec5d")]
    public class LockMenuCommand : IMenuCommand
    {
        private readonly VirtualEngine engine;
        private readonly ILogger logger;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="engine">Engine instance.</param>
        /// <param name="logger">Logger.</param>
        public LockMenuCommand(VirtualEngine engine, ILogger logger)
        {
            this.engine = engine;
            this.logger = logger.CreateLogger(nameof(LockMenuCommand));
        }

        public async Task InvokeAsync(IEnumerable<string> filesPath, IEnumerable<byte[]> remoteStorageItemIds)
        {
            logger.LogMessage($"{nameof(LockMenuCommand)}.{nameof(InvokeAsync)}()", string.Join(",", remoteStorageItemIds.Select(p => Encoding.UTF8.GetString(p))));

            // Call your remote storage here to lock the item.
            // Save the lock token and other lock info received from the remote storage on the client.
            // Supply the lock-token as part of each remote storage update in IFile.WriteAsync() method.
            // Note that the actual lock timout applied by the server may be different from the one requested.

            string lockOwner = engine.CurrentUserPrincipal;
            TimeSpan timeOut = TimeSpan.MaxValue;

            foreach (byte[] remoteStorageItemId in remoteStorageItemIds)
            {                
                IFileSystemItem fileSystemItem = await engine.GetFileSystemItemAsync(remoteStorageItemId, FileSystemItemType.File, null);
                if (fileSystemItem != null)
                {
                    await ((ILock)fileSystemItem).LockAsync(LockMode.Manual, null, default);
                }
            }

        }
    }
}
