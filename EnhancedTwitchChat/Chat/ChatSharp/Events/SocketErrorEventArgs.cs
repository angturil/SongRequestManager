using System;
using System.Net.Sockets;

namespace ChatSharp.Events
{
    /// <summary>
    /// Raised when a SocketError occurs.
    /// </summary>
    public class SocketErrorEventArgs : EventArgs
    {
        /// <summary>
        /// The error that has occured.
        /// </summary>
        public SocketError SocketError { get; set; }

        internal SocketErrorEventArgs(SocketError socketError)
        {
            SocketError = socketError;
        }
    }
}
