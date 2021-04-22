using System;
using System.Collections.Generic;
using System.Text;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Synchronization state.
    /// </summary>
    public enum SynchronizationState
    {
        /// <summary>
        /// Synchronization is enabled.
        /// </summary>
        Enabled,

        /// <summary>
        /// Synchronizing data between user file system and remote storage.
        /// </summary>
        Synchronizing,

        /// <summary>
        /// Waiting for the next synchronization.
        /// </summary>
        Idle,

        /// <summary>
        /// Synchronization disabled.
        /// </summary>
        Disabled
    }

    /// <summary>
    /// Synchronization state change event argument.
    /// </summary>
    public class SynchEventArgs : EventArgs
    {
        /// <summary>
        /// New state of the service.
        /// </summary>
        public SynchronizationState NewState;

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="newState">New synchronization state.</param>
        public SynchEventArgs(SynchronizationState newState)
        {
            this.NewState = newState;
        }
    }

    /// <summary>
    /// Synchronization state change event delegate.
    /// </summary>
    /// <param name="sender"><see cref="FullSyncService"/> instance firing this event.</param>
    /// <param name="synchEventArgs">New synchronization state.</param>
    public delegate void SyncronizationEvent(object sender, SynchEventArgs synchEventArgs);
}
