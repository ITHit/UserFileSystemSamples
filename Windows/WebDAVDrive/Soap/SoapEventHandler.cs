using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebDAVDrive.Soap
{
    /// <summary>
    /// WebDAV message event delegate.
    /// </summary>
    /// <param name="sender"><see cref="ISession"/> instance sending this event.</param>
    /// <param name="e">Event data that contains a message.</param>
    public delegate void SoapEventHandler(SoapMessageEventArgs e);

    /// <summary>
    /// Event message argument. Contains information to be logged.
    /// </summary>
    public class SoapMessageEventArgs : EventArgs
    {
        /// <summary>
        /// Message to be logged.
        /// </summary>
        public string Message;

        /// <summary>
        /// Type of information being logged.
        /// </summary>
        //public LogLevel LogLevel;
    }
}
