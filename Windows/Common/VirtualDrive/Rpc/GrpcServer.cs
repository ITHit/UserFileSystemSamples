using System;
using ITHit.FileSystem.Samples.Common.Windows.Rpc.Generated;
using GrpcDotNetNamedPipes;
using log4net;
using System.IO.Pipes;
using System.Security.Principal;
using System.Security.AccessControl;
using System.IO;

namespace ITHit.FileSystem.Samples.Common.Windows.Rpc
{
    /// <summary>
    /// Provides RPC methods thought name pipes channel and protobuf protocol.
    /// </summary>
    public class GrpcServer : Logger, IDisposable
    {
        private readonly string rpcCommunicationChannelName;

        private IDisposable namedPipeServer;

        private readonly VirtualEngineBase engine;

        public GrpcServer(string rpcCommunicationChannelName, VirtualEngineBase engine, ILog log4net)
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

                var pipeName = Path.Combine(WindowsIdentity.GetCurrent().Owner.Value, rpcCommunicationChannelName);

                NamedPipeServer server = new NamedPipeServer(pipeName, new NamedPipeServerOptions() {
                    CurrentUserOnly = true
                });
                ShellExtensionRpc.BindService(server.ServiceBinder, new GrpcServerServiceImpl(engine, this));
                server.Start();
                namedPipeServer = server;

                LogMessage("Started", pipeName);
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
            }
        }

        // Creates a PipeSecurity that allows users read/write access
        PipeSecurity CreateSystemIOPipeSecurity()
        {
            PipeSecurity pipeSecurity = new PipeSecurity();

            var id = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);

            // Allow Everyone read and write access to the pipe. 
            pipeSecurity.SetAccessRule(new PipeAccessRule(id, PipeAccessRights.ReadWrite, AccessControlType.Allow));

            return pipeSecurity;
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
