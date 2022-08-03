using ITHit.FileSystem.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Processes console commands.
    /// </summary>
    public class ConsoleProcessor
    {
        private readonly Registrar registrar;
        private readonly LogFormatter logFormatter;
        private readonly Commands commands;

        public ConsoleProcessor(Registrar registrar, LogFormatter logFormatter, Commands commands)
        {
            this.registrar = registrar;
            this.logFormatter = logFormatter;
            this.commands = commands;
        }

        /// <summary>
        /// Prints console commands.
        /// </summary>
        public void PrintHelp()
        {
            logFormatter.LogMessage("\n\n ----------------------------------------------------------------------------------------");
            logFormatter.LogMessage("\n Commands:");
            PrintCommandDescription("Spacebar", "Exit without unregistering (simulate reboot).");
            PrintCommandDescription("Esc", "Unregister file system, delete all files/folders and exit.");
            if (FileSystem.Windows.Package.PackageRegistrar.IsRunningWithSparsePackageIdentity())
            {
                PrintCommandDescription("Shift-Esc", "Unregister file system, delete all files/folders, unregister handlers, uninstall developer certificate, unregister sparse package and exit (simulate full uninstall).");
            }
            PrintCommandDescription("e", "Start/stop the Engine and all sync services.");
            PrintCommandDescription("s", "Start/stop synchronization service.");
            PrintCommandDescription("m", "Start/stop remote storage monitor.");
            PrintCommandDescription("d", "Enable/disable debug and performance logging.");
            PrintCommandDescription("l", $"Open log file. ({logFormatter.LogFilePath})");
            PrintCommandDescription("b", "Submit support tickets, report bugs, suggest features. (https://userfilesystem.com/support/)");
            logFormatter.LogMessage("\n ----------------------------------------------------------------------------------------");
        }

        private void PrintCommandDescription(string key, string description)
        {
            logFormatter.LogMessage($"{Environment.NewLine} {key,12} - {description,-25}");
        }

        /// <summary>
        /// Reads and processes console input.
        /// </summary>
        public async Task ProcessUserInputAsync()
        {
            do
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                switch (keyInfo.Key)
                {
                    case ConsoleKey.F1:
                    case ConsoleKey.H:
                        // Print help info.
                        PrintHelp();
                        break;

                    case ConsoleKey.E:
                        // Start/stop the Engine and all sync services.
                        await commands.StartStopEngineAsync();
                        break;

                    case ConsoleKey.S:
                        // Start/stop synchronization.
                        await commands.StartStopSynchronizationAsync();
                        break;

                    case ConsoleKey.D:
                        // Enables/disables debug logging.
                        logFormatter.DebugLoggingEnabled = !logFormatter.DebugLoggingEnabled;
                        break;

                    case ConsoleKey.M:
                        // Start/stop remote storage monitor.
                        await commands.StartStopRemoteStorageMonitorAsync();
                        break;

                    case ConsoleKey.L:
                        // Open log file.
                        Commands.Open(logFormatter.LogFilePath);
                        break;

                    case ConsoleKey.B:
                        // Submit support tickets, report bugs, suggest features.
                        await commands.OpenSupportPortalAsync();
                        break;

                    case ConsoleKey.Escape:
                        // Simulate app uninstall.
                        await commands.StopEngineAsync();
                        bool removeSparsePackage = keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift);
                        await registrar.UnregisterAsync(commands.Engine, removeSparsePackage);
                        return;

                    case ConsoleKey.Spacebar:
                        // Simulate app restart or machine reboot.
                        await commands.AppExitAsync();
                        return;

                    default:
                        break;
                }

            } while (true);
        }
    }
}
