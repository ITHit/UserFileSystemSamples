using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Provider;

using ITHit.FileSystem.Samples.Common.Windows.Rpc;
using ITHit.FileSystem.Samples.Common.Windows.Rpc.Generated;

namespace CommonShellExtensionRpc
{
    public sealed class CustomStateProviderProxy
    {
        public ItemProperty[] GetItemProperties(string itemPath)
        {
            GrpcClient grpcClient = new GrpcClient("VirtualDrive.RPC");

            try
            {
                
                ItemPropertyRequest request = new()
                {
                    Path = itemPath
                };

                var itemPropertyResult = grpcClient.RpcClient.GetItemPropertiesAsync(request).GetAwaiter().GetResult();

                return itemPropertyResult
                    .Properties
                    .Select(i => new ItemProperty(i.Id, i.Value, i.IconResource))
                    .ToArray();

            }
            catch (Exception ex)
            {
                LogErrorRequest request = new()
                {
                    Message = ex.Message,
                    SourcePath = itemPath
                };

                grpcClient.RpcClient.LogError(request);

                return new ItemProperty[] { };
            }
        }
    }
}
