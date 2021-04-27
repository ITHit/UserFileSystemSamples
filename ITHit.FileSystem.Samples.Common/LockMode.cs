using System;
using System.Collections.Generic;
using System.Text;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Indicates how the file was locked and how to unlock the file.
    /// </summary>
    public enum LockMode
    {
        /// <summary>
        /// The file is not locked.
        /// </summary>
        None = 0,

        /// <summary>
        /// The file is automatically locked on file handle open and should be automatically unlocked on file handle close.
        /// </summary>
        Auto = 1,

        /// <summary>
        /// The file is manually locked by the user and should be manually unlocked by the user.
        /// </summary>
        Manual = 2
    }
}
