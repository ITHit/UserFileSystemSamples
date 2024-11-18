using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebDAVDrive.Services
{
    public interface IDomainsService
    {
        AppSettings Settings { get; }
        Registrar Registrar { get; }
        LogFormatter LogFormatter { get; }
        ConsoleProcessor ConsoleProcessor { get; }
        IToastNotificationService NotificationService { get; }
        ConcurrentDictionary<Guid, VirtualEngine> Engines { get; }
        ConcurrentDictionary<Guid, EngineWindows> GetEngineWindowsDictionary();
        Task MountNewAsync(string[] webDAVServerURLs);
        Task UnMountAsync(Guid engineId, string webDAVServerURL);
        Task InitializeAsync(bool displayMountNewDriveWindow);
        Task EnginesExitAsync();
        Task<VirtualEngine?> EnsureEngineMountedAsync(Uri mountUrl);
    }
}
