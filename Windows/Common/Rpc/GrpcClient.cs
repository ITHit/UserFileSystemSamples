using System;
using ITHit.FileSystem.Samples.Common.Windows.Rpc.Generated;
using GrpcDotNetNamedPipes;

namespace ITHit.FileSystem.Samples.Common.Windows.Rpc
{
    /// <summary>
    /// GrpcClient establish connection to rpc server and provides api methods.
    /// </summary>
    public class GrpcClient
    {
        private static string rpcCommunicationChannelName = null;

        public GrpcClient(string channelName)
        {
            if (string.IsNullOrEmpty(rpcCommunicationChannelName))
                rpcCommunicationChannelName = channelName;
        }

        public const int ConnectionTimeoutMs = 1000;

        private static Lazy<ShellExtensionRpc.ShellExtensionRpcClient> namedPipeChannel = new Lazy<ShellExtensionRpc.ShellExtensionRpcClient>(Connect);

        /// <summary>
        /// Returns rpc client.
        /// </summary>
        public ShellExtensionRpc.ShellExtensionRpcClient RpcClient => namedPipeChannel.Value;

        /// <summary>
        /// Establish connection.
        /// </summary>
        private static ShellExtensionRpc.ShellExtensionRpcClient Connect()
        {
            NamedPipeChannelOptions options = new NamedPipeChannelOptions
            {
                ConnectionTimeout = ConnectionTimeoutMs,
                CurrentUserOnly = true,
                ImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.None
            };

            NamedPipeChannel channel = new NamedPipeChannel(".", rpcCommunicationChannelName, options);
            ShellExtensionRpc.ShellExtensionRpcClient client = new ShellExtensionRpc.ShellExtensionRpcClient(channel);
            return client;
        }
    }
}
