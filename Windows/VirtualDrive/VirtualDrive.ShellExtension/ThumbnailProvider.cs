using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Thumbnails;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Interop;
using ITHit.FileSystem.Samples.Common.Windows.Rpc;
using ITHit.FileSystem.Samples.Common.Windows.Rpc.Generated;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension;

namespace VirtualDrive.ShellExtension
{
    // It is Windows Shell Extension code
    [ComVisible(true)]
    [ProgId("VirtualDrive.ThumbnailProvider"), Guid(ThumbnailClass)]
    public class ThumbnailProvider : ThumbnailProviderBase
    {
        public const string ThumbnailClass = "05CF065E-E135-4B2B-9D4D-CFB3FBAC73A4";
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
