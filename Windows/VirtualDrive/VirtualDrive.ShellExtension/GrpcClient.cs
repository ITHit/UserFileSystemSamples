using System;
using VirtualDrive.Rpc.Generated;
using GrpcDotNetNamedPipes;
using VirtualDrive.ShellExtension.Settings;

namespace VirtualDrive.ShellExtension
{
    /// <summary>
    /// GrpcClient establish connection to rpc server and provides api methods.
    /// </summary>
    public class GrpcClient
    {
        private static Lazy<VirtualDriveRpc.VirtualDriveRpcClient> namedPipeChannel = new Lazy<VirtualDriveRpc.VirtualDriveRpcClient>(Connect);

        /// <summary>
        /// Returns rpc client.
        /// </summary>
        public VirtualDriveRpc.VirtualDriveRpcClient RpcClient => namedPipeChannel.Value;

        /// <summary>
        /// Establish connection.
        /// </summary>
        private static VirtualDriveRpc.VirtualDriveRpcClient Connect()
        {
            AppSettings settings = AppSettings.Load();

            NamedPipeChannel channel = new NamedPipeChannel(".", settings.RpcCommunicationChannelName);
            VirtualDriveRpc.VirtualDriveRpcClient client = new VirtualDriveRpc.VirtualDriveRpcClient(channel);
            return client;
        }
    }
}
