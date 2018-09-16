using System;

namespace ChatSharp.Events
{
    /// <summary>
    /// Describes a raw IRC message we have sent or received.
    /// </summary>
    public class RawMessageEventArgs : EventArgs
    {
        /// <summary>
        /// The text of the raw IRC message.
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// True if this message is going from ChatSharp to the server.
        /// </summary>
        public bool Outgoing { get; set; }

        internal RawMessageEventArgs(string message, bool outgoing)
        {
            Message = message;
            Outgoing = outgoing;
        }
    }
}
