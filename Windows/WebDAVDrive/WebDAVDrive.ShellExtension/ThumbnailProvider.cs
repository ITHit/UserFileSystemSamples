using System;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Thumbnails;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Interop;
using ITHit.FileSystem.Samples.Common.Windows.Rpc;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension;
using ITHit.FileSystem.Samples.Common.Windows.Rpc.Generated;

namespace WebDAVDrive.ShellExtension
{
    // It is Windows Shell Extension code
    [ComVisible(true)]
    [ProgId("WebDAVDrive.ThumbnailProvider"), Guid(ThumbnailClass)]
    public class ThumbnailProvider : ThumbnailProviderBase
    {
        public const string ThumbnailClass = "A5B0C82F-50AA-445C-A404-66DEB510E84B";
        public static readonly Guid ThumbnailClassGuid = Guid.Parse(ThumbnailClass);

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
