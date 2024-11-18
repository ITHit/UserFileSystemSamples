using log4net;
using log4net.Config;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace WebDAVDrive.Extensions
{
    public static class Log4NetExtensions
    {
        public static ILoggingBuilder AddLog4Net(this ILoggingBuilder builder, string log4NetConfigFile)
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());

            var fileInfo = new FileInfo(log4NetConfigFile);
            XmlConfigurator.Configure(logRepository, fileInfo);

            builder.AddProvider(new Log4NetProvider());
            return builder;
        }
    }
}
