using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ITHit.FileSystem.Samples.Common.Windows.Rpc;
using ITHit.FileSystem.Samples.Common.Windows.Rpc.Generated;

namespace CommonShellExtensionRpc
{
    public sealed class UriSourceProxy
    {
        public StorageProviderGetPathForContentUriResult GetPathForContentUri(string contentUri)
        {
            string channelName = "VirtualDrive.RPC";

            GrpcClient grpcClient = new GrpcClient(channelName);

            try
            {
                UriSourceRequest request = new()
                {
                    PathOrUri = contentUri
                };

                GetPathForContentUriResult result = grpcClient.RpcClient.GetPathForContentUriAsync(request).GetAwaiter().GetResult();

                return new StorageProviderGetPathForContentUriResult(result.Path, result.Status);
            }
            catch (Exception ex)
            {
                LogErrorRequest request = new()
                {
                    Message = ex.Message,
                    SourcePath = contentUri
                };

                grpcClient.RpcClient.LogError(request);
            }

            return new StorageProviderGetPathForContentUriResult("", 0);
        }

        public StorageProviderGetContentInfoForPathResult GetContentInfoForPath(string path)
        {
            string channelName = "VirtualDrive.RPC";

            GrpcClient grpcClient = new GrpcClient(channelName);

            try
            {
                UriSourceRequest request = new()
                {
                    PathOrUri = path
                };

                GetContentInfoForPathResult result = grpcClient.RpcClient.GetContentInfoForPathAsync(request).GetAwaiter().GetResult();

                return new StorageProviderGetContentInfoForPathResult(result.ContentId, result.ContentUri, result.Status);
            }
            catch (Exception ex)
            {
                LogErrorRequest request = new()
                {
                    Message = ex.Message,
                    SourcePath = path
                };

                grpcClient.RpcClient.LogError(request);
            }

            return new StorageProviderGetContentInfoForPathResult("", "", 0);
        }
    }
}
