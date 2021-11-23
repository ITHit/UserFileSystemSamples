using System;
using System.Threading;

namespace ITHit.FileSystem.Samples.Common.Windows.ShellExtension.ComInfrastructure
{
    /// <summary>
    /// GcReferencesCleaner forces to cleanup references.
    /// It allows to have actual state of Com objects.
    /// </summary>
    internal class GcReferencesCleaner : IDisposable
    {
        private const int CheckIntervalMs = 10000;

        private readonly Timer timer;

        public GcReferencesCleaner()
        {
            timer = new Timer(o => GC.Collect(), null, CheckIntervalMs, CheckIntervalMs);
        }

        public void Dispose()
        {
            timer.Dispose();
        }
    }
}
