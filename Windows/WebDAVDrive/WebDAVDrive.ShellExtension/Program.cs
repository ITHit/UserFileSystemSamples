using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension.ComInfrastructure;
using ITHit.FileSystem.Samples.Common;
using System.Reflection;
using System.IO;
using System.Linq;
using log4net;
using log4net.Config;
using log4net.Appender;


namespace WebDAVDrive.ShellExtension
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Typically you will register COM exe server in packaged installer, inside Package.appxmanifest.
            // This code is used for testing purposes only, it will register COM exe if you run this program directly.  
            if (args.Length == 1 &&
                ShellExtensionInstaller.HandleRegCommand(args[0], ThumbnailProvider.ThumbnailClassGuid, ContextMenusProvider.ContextMenusClassGuid))
            {
                return;
            }

            // Load and initialize settings.
            //ShellExtensionConfiguration.Initialize();
            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build();
            Settings settings = new Settings();
            configuration.Bind(settings);

            ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            ConfigureLogger(settings);

            ShellExtensionConfiguration.Initialize(settings, log);

            try
            {
                using (var server = new LocalServer())
                {
                    server.RegisterClass<ThumbnailProvider>(ThumbnailProvider.ThumbnailClassGuid);
                    server.RegisterClass<ContextMenusProvider>(ContextMenusProvider.ContextMenusClassGuid);

                    await server.Run();
                }
            }
            catch (Exception ex)
            {
                log.Error("", ex);
            }
        }

        /// <summary>
        /// Configures log4net logger.
        /// </summary>
        /// <returns>Log file path.</returns>
        private static void ConfigureLogger(Settings settings)
        {
            // Load Log4Net for net configuration.
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "log4net.config")));

            // Update log file path for msix package. 
            RollingFileAppender rollingFileAppender = logRepository.GetAppenders().Where(p => p.GetType() == typeof(RollingFileAppender)).FirstOrDefault() as RollingFileAppender;
            if (rollingFileAppender != null && rollingFileAppender.File.Contains("WindowsApps"))
            {
                rollingFileAppender.File = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), settings.AppID+".ShellExtension",
                                                        Path.GetFileName(rollingFileAppender.File));
            }
        }
    }
}
