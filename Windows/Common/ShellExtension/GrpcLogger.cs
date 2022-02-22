using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

using ITHit.FileSystem.Samples.Common.Windows.Rpc;
using ITHit.FileSystem.Samples.Common.Windows.Rpc.Generated;

namespace ITHit.FileSystem.Samples.Common.Windows.ShellExtension
{
    public class GrpcLogger : ILogger
    {
        private readonly string componentName;

        private GrpcClient grpcClient = new GrpcClient(ShellExtensionConfiguration.AppSettings.RpcCommunicationChannelName);

        public GrpcLogger(string componentName)
        {
            this.componentName = componentName ?? throw new ArgumentNullException(nameof(componentName));
        }

        public void LogError(string message, string sourcePath = null, string targetPath = null, Exception ex = null, IOperationContext operationContext = null)
        {
            LogErrorRequest request = new();
            request.ComponentName = componentName;
            request.Message = message ?? "";
            request.SourcePath = sourcePath ?? "";
            request.TargetPath = targetPath ?? "";
            request.ExSerialized = ex.ToString();
            grpcClient.RpcClient.LogError(request);
        }

        public void LogMessage(string message, string sourcePath = null, string targetPath = null, IOperationContext operationContext = null)
        {
            LogMessageRequest request = new();
            request.ComponentName = componentName;
            request.Message = message ?? "";
            request.SourcePath = sourcePath ?? "";
            request.TargetPath = targetPath ?? "";
            grpcClient.RpcClient.LogMessage(request);
        }

        public static string XmlSerialize<T>(T toSerialize)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
            using (StringWriter textWriter = new StringWriter())
            {
                xmlSerializer.Serialize(textWriter, toSerialize);
                return textWriter.ToString();
            }
        }
    }

    public class GrpcException: Exception
    {
        public GrpcException(Exception ex):base("", ex)
        {}

        public override IDictionary Data { get { return null; } }
    }

    public class GrpcExceptionRequest
    {
        public string Message { get; set; }
        public string StackTrace { get; set; }

        public GrpcExceptionRequest(Exception ex)
        {
            Message = ex.Message;
            StackTrace = ex.StackTrace;

            Exception ex1 = new Exception();
        }
    }
}
