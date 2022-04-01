using System;
using ITHit.FileSystem.Samples.Common.Windows.Rpc.Generated;
using GrpcDotNetNamedPipes;
using System.IO;
using System.Security.Principal;

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
                ImpersonationLevel = TokenImpersonationLevel.None
            };

            var pipeName = Path.Combine(WindowsIdentity.GetCurrent().Owner.Value, rpcCommunicationChannelName);

            NamedPipeChannel channel = new NamedPipeChannel(".", pipeName, options);
            ShellExtensionRpc.ShellExtensionRpcClient client = new ShellExtensionRpc.ShellExtensionRpcClient(channel);
            return client;
        }
    }
}
