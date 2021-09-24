using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

namespace VirtualDrive.ShellExtension
{
    public static class ShelIExtensionModule
    {
        /// <summary>
        /// Returns logger with configured repository.
        /// </summary>
        public static ILog GetLogger(string logName)
        {
            string assemblyPath = Path.GetDirectoryName(typeof(ShelIExtensionModule).Assembly.Location);
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository(typeof(ShelIExtensionModule).Assembly);

            if (!hierarchy.Configured)
            {
                PatternLayout patternLayout = new PatternLayout();
                patternLayout.ConversionPattern = "%date [%thread] %-5level %logger - %message%newline";
                patternLayout.ActivateOptions();

                RollingFileAppender roller = new RollingFileAppender();
                roller.AppendToFile = true;
                if (assemblyPath.Contains("WindowsApps"))
                {
                    roller.File = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        Mapping.AppSettings.AppID), "ShelIExtension.log");
                }
                else
                {
                    roller.File = Path.Combine(assemblyPath, "ShelIExtension.log");
                }
                roller.Layout = patternLayout;
                roller.MaxSizeRollBackups = 5;
                roller.MaximumFileSize = "10MB";
                roller.RollingStyle = RollingFileAppender.RollingMode.Size;
                roller.StaticLogFileName = true;
                roller.ActivateOptions();
                hierarchy.Root.AddAppender(roller);

                hierarchy.Root.Level = Level.Info;
                hierarchy.Configured = true;
            }

            return LogManager.GetLogger(typeof(ShelIExtensionModule));
        }


    }
}
