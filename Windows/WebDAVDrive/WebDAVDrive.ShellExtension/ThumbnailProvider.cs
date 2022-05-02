using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Thumbnails;
using ITHit.FileSystem.Samples.Common.Windows.Rpc;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension;
using ITHit.FileSystem.Samples.Common.Windows.Rpc.Generated;

namespace WebDAVDrive.ShellExtension
{
    /// <summary>
    /// Thumbnails provider Windows Shell Extension.
    /// </summary>
    [ComVisible(true)]
    [ProgId("WebDAVDrive.ThumbnailProvider")]
    [Guid("A5B0C82F-50AA-445C-A404-66DEB510E84B")]
    public class ThumbnailProvider : ThumbnailProviderBase
    {
        public override async Task<byte[]> GetThumbnailsAsync(string filePath, uint size)
        {
            try
            {
                GrpcClient grpcClient = new GrpcClient(ShellExtensionConfiguration.AppSettings.RpcCommunicationChannelName);
                ThumbnailRequest thumbnailRequest = new ThumbnailRequest();
                thumbnailRequest.Path = filePath;
                thumbnailRequest.Size = size;

                Thumbnail thumbnail = await grpcClient.RpcClient.GetThumbnailAsync(thumbnailRequest);

                return thumbnail.Image.ToByteArray();
            }
            catch (Exception ex)
            {
                throw new NotImplementedException(ex.Message);
            }
        }
    }
}
