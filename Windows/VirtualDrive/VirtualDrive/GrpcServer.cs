using System;
using System.Threading;
using System.Threading.Tasks;
using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using VirtualDrive.Rpc.Generated;
using GrpcDotNetNamedPipes;
using ITHit.FileSystem.Samples.Common.Windows;
using log4net;

namespace VirtualDrive
{
    /// <summary>
    /// Provides RPC methods thought name pipes channel and protobuf protocol.
    /// </summary>
    public class GrpcServer : Logger, IDisposable
    {
        private readonly string rpcCommunicationChannelName;

        private IDisposable namedPipeServer;

        private readonly EngineWindows engine;

        public GrpcServer(string rpcCommunicationChannelName, EngineWindows engine, ILog log4net)
            : base("gRPC Server", log4net)
        {
            this.rpcCommunicationChannelName = rpcCommunicationChannelName;

            this.engine = engine;
        }

        /// <summary>
        /// Starts server and bind RPC methods handler.
        /// </summary>
        public void Start()
        {
            try
            {
                if (namedPipeServer != null)
                {
                    Stop();
                }

                NamedPipeServer server = new NamedPipeServer(rpcCommunicationChannelName);
                VirtualDriveRpc.BindService(server.ServiceBinder, new GprcServerServiceImpl(engine, this));
                server.Start();
                namedPipeServer = server;

                LogMessage("Started", rpcCommunicationChannelName);
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
            }
        }

        /// <summary>
        /// Stops server.
        /// </summary>
        public void Stop()
        {
            if (namedPipeServer != null)
            {
                namedPipeServer.Dispose();
                namedPipeServer = null;

                LogMessage("Stopped", rpcCommunicationChannelName);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
