using System;

namespace ChatSharp.Events
{
    /// <summary>
    /// Raised when we have received the MOTD from the server.
    /// </summary>
    public class ServerMOTDEventArgs : EventArgs
    {
        /// <summary>
        /// The message of the day.
        /// </summary>
        public string MOTD { get; set; }

        internal ServerMOTDEventArgs(string motd)
        {
            MOTD = motd;
        }
    }
}
