using System;
using System.Drawing;
using System.Threading.Tasks;
using System.IO;

using ITHit.FileSystem.Samples.Common.Windows.Rpc;
using ITHit.FileSystem.Samples.Common.Windows.Rpc.Generated;

namespace ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Thumbnails
{
    public class ThumbnailProviderCommon : ThumbnailProviderBase
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
