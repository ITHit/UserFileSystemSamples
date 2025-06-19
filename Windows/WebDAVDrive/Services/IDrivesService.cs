using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;

namespace WebDAVDrive.Services
{
    /// <summary>
    /// Contains globally available list of drives, drive mounting API and app settings.
    /// </summary>
    public interface IDrivesService
    {
        AppSettings Settings { get; }
        Registrar Registrar { get; }
        LogFormatter LogFormatter { get; }
        ConsoleProcessor ConsoleProcessor { get; }
        IToastNotificationService NotificationService { get; }
        ConcurrentDictionary<Guid, VirtualEngine> Engines { get; }
        ConcurrentDictionary<Guid, EngineWindows> GetEngineWindowsDictionary();
        Task<(bool success, Exception? exception)> MountNewAsync(string webDAVServerUrl);
        Task UnMountAsync(Guid? engineId);
        Task InitializeAsync(bool displayMountNewDriveWindow);
        Task EnginesExitAsync();
        Task<VirtualEngine?> EnsureEngineMountedAsync(Uri mountUrl);
    }
}
