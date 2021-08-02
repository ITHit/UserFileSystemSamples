using System;
using System.Collections.Generic;
using System.Text;

namespace VirtualDrive.ThumbnailProvider
{
    public delegate void ReferenceEventHandler();

    /// <summary>
    /// ReferenceManager provides centralised methods to notify that new com objects created/released or server locked/unlocked.
    /// </summary>
    public static class ReferenceManager
    {
        /// <summary>
        /// Notify that com object was created.
        /// </summary>
        public static void AddObjectReference()
        {
            ObjectCreated?.Invoke();
        }

        /// <summary>
        /// Notify that com object was released.
        /// </summary>
        public static void ReleaseObjectReference()
        {
            ObjectDestroyed?.Invoke();
        }

        /// <summary>
        /// Notify that server was locked.
        /// </summary>
        public static void LockServer()
        {
            ServerLocked?.Invoke();
        }

        /// <summary>
        /// Notify that server was unlocked.
        /// </summary>
        public static void UnlockServer()
        {
            ServerUnlocked?.Invoke();
        }

        public static event ReferenceEventHandler ObjectCreated;
        public static event ReferenceEventHandler ObjectDestroyed;

        public static event ReferenceEventHandler ServerLocked;
        public static event ReferenceEventHandler ServerUnlocked;
    }
}
