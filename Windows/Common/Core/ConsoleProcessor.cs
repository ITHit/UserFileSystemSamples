using ITHit.FileSystem.Windows;
using System;
using System.Collections.Concurrent;
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
        public readonly ConcurrentDictionary<Guid, Commands> Commands = new ConcurrentDictionary<Guid, Commands>();
        private readonly Registrar registrar;
        private readonly LogFormatter logFormatter;
        private readonly string providerId;

        public ConsoleProcessor(Registrar registrar, LogFormatter logFormatter, string providerId)
        {
            this.registrar = registrar;
            this.logFormatter = logFormatter;
            this.providerId = providerId;
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
        public async Task ProcessUserInputAsync(Action? onAppExit = null)
        {
            do
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                switch (keyInfo.Key)
                {
                    //case ConsoleKey.X:
                    //    foreach (var keyValCommands in Commands)
                    //    {
                    //        keyValCommands.Value.Test();
                    //    }
                    //    break;

                    case ConsoleKey.F1:
                    case ConsoleKey.H:
                        // Print help info.
                        PrintHelp();
                        break;

                    case ConsoleKey.E:
                        // Start/stop the Engine and all sync services.
                        foreach (var keyValCommands in Commands)
                        {
                            await keyValCommands.Value.StartStopEngineAsync();
                        }
                        break;

                    case ConsoleKey.S:
                        // Start/stop synchronization.
                        foreach (var keyValCommands in Commands)
                        {
                            await keyValCommands.Value.StartStopSynchronizationAsync();
                        }
                        break;

                    case ConsoleKey.D:
                        // Enables/disables debug logging.
                        logFormatter.DebugLoggingEnabled = !logFormatter.DebugLoggingEnabled;
                        break;

                    case ConsoleKey.M:
                        // Start/stop remote storage monitor.
                        foreach (var keyValCommands in Commands)
                        {
                            await keyValCommands.Value.StartStopRemoteStorageMonitorAsync();
                        }
                        break;

                    case ConsoleKey.L:
                        // Open log file.
                        Windows.Commands.TryOpen(logFormatter.LogFilePath);
                        break;

                    case ConsoleKey.B:
                        // Submit support tickets, report bugs, suggest features.
                        await Windows.Commands.OpenSupportPortalAsync();
                        break;

                    case ConsoleKey.Escape:
                        // Simulate app uninstall.
                        foreach (var keyValCommands in Commands)
                        {
                            await keyValCommands.Value.StopEngineAsync();
                        }

                        bool removeSparsePackage = FileSystem.Windows.Package.PackageRegistrar.IsRunningWithSparsePackageIdentity() ?
                            keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift) : false;
                        await registrar.UnregisterAllSyncRootsAsync(this.providerId, removeSparsePackage);

                        if (onAppExit != null)
                        {
                            onAppExit();
                        }
                        return;

                    case ConsoleKey.Spacebar:
                        // Simulate app restart or machine reboot.
                        foreach (var keyValCommands in Commands)
                        {
                            await keyValCommands.Value.EngineExitAsync();
                        }

                        if (onAppExit != null)
                        {
                            onAppExit();
                        }
                        return;

                    default:
                        break;
                }

            } while (true);
        }
    }
}
