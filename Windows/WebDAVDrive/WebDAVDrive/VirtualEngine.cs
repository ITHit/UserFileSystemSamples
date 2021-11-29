using System.IO;
using System.Threading.Tasks;
using log4net;
using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using System;
using System.Net;
using System.Linq;

namespace WebDAVDrive
{
    /// <inheritdoc />
    public class VirtualEngine : VirtualEngineBase
    {
        /// <summary>
        /// Monitors changes in the remote storage, notifies the client and updates the user file system.
        /// </summary>
        internal readonly RemoteStorageMonitor RemoteStorageMonitor;

        /// <summary>
        /// Gets or sets a value that indicates whether to send an authenticate header with the websocket.
        /// </summary>
        public NetworkCredential Credentials { get; set; }

        /// <summary>
        /// Creates a vitual file system Engine.
        /// </summary>
        /// <param name="license">A license string.</param>
        /// <param name="userFileSystemRootPath">
        /// A root folder of your user file system. 
        /// Your file system tree will be located under this folder.
        /// </param>
        /// <param name="remoteStorageRootPath">Path to the remote storage root.</param>
        /// <param name="serverDataFolderPath">Path to the folder that stores custom data associated with files and folders.</param>
        /// <param name="iconsFolderPath">Path to the icons folder.</param>
        /// <param name="rpcCommunicationChannelName">Channel name to communicate with Windows Explorer context menu and other components on this machine.</param>
        /// <param name="syncIntervalMs">Full synchronization interval in milliseconds.</param>
        /// <param name="log4net">Log4net logger.</param>
        public VirtualEngine(
            string license, 
            string userFileSystemRootPath, 
            string remoteStorageRootPath, 
            string serverDataFolderPath, 
            string webSocketServerUrl, 
            string iconsFolderPath, 
            string rpcCommunicationChannelName,
            double syncIntervalMs,
            ILog log4net)
            : base(license, userFileSystemRootPath, remoteStorageRootPath, serverDataFolderPath, iconsFolderPath, rpcCommunicationChannelName, syncIntervalMs, log4net)
        {
            RemoteStorageMonitor = new RemoteStorageMonitor(webSocketServerUrl, this, log4net);
        }

        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(string userFileSystemPath, FileSystemItemType itemType, byte[] itemId)
        {
            if (itemType == FileSystemItemType.File)
            {
                return new VirtualFile(userFileSystemPath, this, this);
            }
            else
            {
                return new VirtualFolder(userFileSystemPath, this, this);
            }
        }

        public override IMapping Mapping { get { return new Mapping(this); } }

        /// <inheritdoc/>
        public override async Task StartAsync()
        {
            await base.StartAsync();
            await RemoteStorageMonitor.StartAsync();
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();
            await RemoteStorageMonitor.StopAsync();
        }

        private bool disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    RemoteStorageMonitor.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Returns thumbnail for item.
        /// Or throw NotImplementedException if thumbnail is not available.
        /// Also as option might be returned empty array of null as indication of non existed thumbnail.
        /// </summary>
        public override async Task<byte[]> GetThumbnailAsync(string path, uint size)
        {
            string[] exts = Program.Settings.RequestThumbnailsFor.Trim().Split("|");
            string ext = System.IO.Path.GetExtension(path).TrimStart('.');

            if (exts.Any(ext.Equals) || exts.Any("*".Equals))
            {
                string ThumbnailGeneratorUrl = Program.Settings.ThumbnailGeneratorUrl.Replace("{thumbnail width}", ""+size).Replace("{thumbnail height}", "" + size);
                string filePathRemote = ThumbnailGeneratorUrl.Replace("{path to file}", WebDAVDrive.Mapping.MapPath(path));

                try
                {
                    using Stream stream = await Program.DavClient.DownloadAsync(new Uri(filePathRemote));
                    return await StreamToByteArrayAsync(stream);
                }
                catch (WebException we)
                {
                    LogMessage(we.Message, path);
                }
                catch (Exception e)
                {
                    LogError("Failed to load thumbnail", path, null, e);
                }
            }
            return null;
        }

        private static async Task<byte[]> StreamToByteArrayAsync(Stream stream)
        {
            using (MemoryStream memoryStream = new())
            {
                await stream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }
    }
}
