using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VirtualDrive.ThumbnailProvider.Interop;

namespace VirtualDrive.ThumbnailProvider.ComInfrastructure
{
    /// <summary>
    /// LocalServer encapsulates Com Exe server lifecycle
    /// from Com class registration till exit if no object references or server locks.
    /// </summary>
    public class LocalServer : IDisposable
    {
        private readonly List<int> registrationCookies = new List<int>();

        private int objectRefs = 0;
        private int serverLocks = 0;

        private GcReferencesCleaner referenceCleaner = new GcReferencesCleaner();

        private object exitLock = new object();

        private TaskCompletionSource<bool> completionSource;

        public LocalServer()
        {
            ReferenceManager.ObjectCreated += OnObjectCreated;
            ReferenceManager.ObjectDestroyed += OnObjectDestroyed;

            ReferenceManager.ServerLocked += OnServerLocked;
            ReferenceManager.ServerUnlocked += OnServerUnlocked;
        }

        /// <summary>
        /// Com class registration.
        /// </summary>
        public void RegisterClass<T>(Guid clsid) where T : new()
        {
            int cookie;
            int hr = Ole32.CoRegisterClassObject(ref clsid, new BasicClassFactory<T>(), Ole32.CLSCTX_LOCAL_SERVER, Ole32.REGCLS_MULTIPLEUSE | Ole32.REGCLS_SUSPENDED, out cookie);
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            registrationCookies.Add(cookie);

            hr = Ole32.CoResumeClassObjects();
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        /// <summary>
        /// Returns task with lidecycle of com server.
        /// </summary>
        public async Task<bool> Run()
        {
            completionSource = new TaskCompletionSource<bool>();

            return await completionSource.Task;
        }

        /// <summary>
        /// Performs application-defined tasks associated with releasing resources.
        /// </summary>
        public void Dispose()
        {
            foreach (int cookie in registrationCookies)
            {
                int hr = Ole32.CoRevokeClassObject(cookie);
                Debug.Assert(hr >= 0, $"CoRevokeClassObject failed ({hr:x}). Cookie: {cookie}");
            }

            ReferenceManager.ObjectCreated -= OnObjectCreated;
            ReferenceManager.ObjectDestroyed -= OnObjectDestroyed;

            ReferenceManager.ServerLocked -= OnServerLocked;
            ReferenceManager.ServerUnlocked -= OnServerUnlocked;

            referenceCleaner.Dispose();
        }

        /// <summary>
        /// Handles com object creation.
        /// </summary>
        private void OnObjectCreated()
        {
            lock (exitLock)
            {
                objectRefs++;
            }
        }

        /// <summary>
        /// Handles com object release.
        /// </summary>
        private void OnObjectDestroyed()
        {
            lock (exitLock)
            {
                objectRefs--;

                ExitIfNoRefs();
            }
        }

        /// <summary>
        /// Handles com server lock.
        /// </summary>
        private void OnServerLocked()
        {
            lock (exitLock)
            {
                serverLocks++;
            }
        }

        /// <summary>
        /// Handles com server unlock.
        /// </summary>
        private void OnServerUnlocked()
        {
            lock (exitLock)
            {
                serverLocks--;

                ExitIfNoRefs();
            }
        }

        /// <summary>
        /// Checks condition to exit and complete lifecycle task.
        /// </summary>
        private void ExitIfNoRefs()
        {
            if ((objectRefs <= 0) && (serverLocks <= 0))
            {
                completionSource.SetResult(true);
            }
        }
    }
}
