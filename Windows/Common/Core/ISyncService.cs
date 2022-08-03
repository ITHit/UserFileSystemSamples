using System.Threading.Tasks;
using ITHit.FileSystem.Windows;


namespace ITHit.FileSystem.Samples.Common.Windows
{
    public interface ISyncService
    {
        /// <summary>
        /// Sync service state.
        /// </summary>
        SynchronizationState SyncState { get; }

        /// <summary>
        /// Starts service.
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops service.
        /// </summary>
        Task StopAsync();
    }
}
