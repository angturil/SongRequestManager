using System;

namespace ChatSharp.Events
{
    /// <summary>
    /// Raised when a user has changed their nick.
    /// </summary>
    public class NickChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The user whose nick changed.
        /// </summary>
        public IrcUser User { get; set; }
        /// <summary>
        /// The original nick.
        /// </summary>
        public string OldNick { get; set; }
        /// <summary>
        /// The new nick.
        /// </summary>
        public string NewNick { get; set; }
    }
}