using System;

namespace ChatSharp.Events
{
    /// <summary>
    /// Generic event args for events regarding users in channels.
    /// </summary>
    public class ChannelUserEventArgs : EventArgs
    {
        /// <summary>
        /// The channel this event regards.
        /// </summary>
        public IrcChannel Channel { get; set; }
        /// <summary>
        /// The user this event regards.
        /// </summary>
        public IrcUser User { get; set; }

        internal ChannelUserEventArgs(IrcChannel channel, IrcUser user)
        {
            Channel = channel;
            User = user;
        }
    }
}
