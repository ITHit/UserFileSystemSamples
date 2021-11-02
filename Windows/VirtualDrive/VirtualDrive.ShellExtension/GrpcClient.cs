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
        public const int ConnectionTimeoutMs = 1000;

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

            NamedPipeChannelOptions options = new NamedPipeChannelOptions
            {
                ConnectionTimeout = ConnectionTimeoutMs,
                CurrentUserOnly = true,
                ImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.None
            };

            NamedPipeChannel channel = new NamedPipeChannel(".", settings.RpcCommunicationChannelName, options);
            VirtualDriveRpc.VirtualDriveRpcClient client = new VirtualDriveRpc.VirtualDriveRpcClient(channel);
            return client;
        }
    }
}
