using System;

namespace ChatSharp.Events
{
    /// <summary>
    /// Generic event args for events regarding channels.
    /// </summary>
    public class ChannelEventArgs : EventArgs
    {
        /// <summary>
        /// The channel this event regards.
        /// </summary>
        public IrcChannel Channel { get; set; }

        internal ChannelEventArgs(IrcChannel channel)
        {
            Channel = channel;
        }
    }
}
