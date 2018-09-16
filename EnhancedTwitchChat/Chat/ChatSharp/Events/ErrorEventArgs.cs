using System;
using System.Net.Sockets;

namespace ChatSharp.Events
{
    /// <summary>
    /// Raised when a Error occurs.
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        /// <summary>
        /// The error that has occured.
        /// </summary>
        public Exception Error { get; set; }

        internal ErrorEventArgs(Exception error)
        {
            Error = error;
        }
    }
}
