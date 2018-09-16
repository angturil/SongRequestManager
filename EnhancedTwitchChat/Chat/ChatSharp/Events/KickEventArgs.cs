using System;

namespace ChatSharp
{
    /// <summary>
    /// Raised when a user (possibly yourself) is kicked from a channel.
    /// </summary>
    public class KickEventArgs : EventArgs
    {
        internal KickEventArgs(IrcChannel channel, IrcUser kicker, IrcUser kicked, string reason)
        {
            Channel = channel;
            Kicker = kicker;
            Kicked = kicked;
            Reason = reason;
        }

        /// <summary>
        /// The channel the user was kicked from.
        /// </summary>
        public IrcChannel Channel { get; set; }
        /// <summary>
        /// The user who issued the kick.
        /// </summary>
        public IrcUser Kicker { get; set; }
        /// <summary>
        /// The user that was kicked.
        /// </summary>
        public IrcUser Kicked { get; set; }
        /// <summary>
        /// The reason provided for the kick (may be null).
        /// </summary>
        public string Reason { get; set; }   
    }
}

