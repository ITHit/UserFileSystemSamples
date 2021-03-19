using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ITHit.FileSystem.Samples.Common
{
    /// <summary>
    /// Thrown when a file can not be locked. For example when a lock-token file is blocked 
    /// from another thread, during update, lock and unlock operations.  
    /// </summary>
    public class ClientLockFailedException : IOException
    {
        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public ClientLockFailedException(string message) : base(message)
        {
        }

        /// <summary>
        /// Creates instance of this class with a specified
        /// error message and a reference to the inner exception that is the cause of this
        /// exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception. If the innerException
        /// parameter is not null, the current exception is raised in a catch block that
        /// handles the inner exception.
        /// </param>
        public ClientLockFailedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Creates instance of this class with a message and HRESULT code.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="hresult">An integer identifying the error that has occurred.</param>
        public ClientLockFailedException(string message, int hresult) : base(message, hresult)
        {
        }
    }
}
