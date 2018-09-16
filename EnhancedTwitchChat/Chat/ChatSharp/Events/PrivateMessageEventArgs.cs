using System;

namespace ChatSharp.Events
{
    /// <summary>
    /// Describes a private message we have received.
    /// The term "private message" is misleading - this describes both messages sent user-to-user,
    /// and messages sent to a channel.
    /// </summary>
    public class PrivateMessageEventArgs : EventArgs
    {
        /// <summary>
        /// The IRC message received.
        /// </summary>
        public IrcMessage IrcMessage { get; set; }
        /// <summary>
        /// The private message received.
        /// </summary>
        public PrivateMessage PrivateMessage { get; set; }

        internal PrivateMessageEventArgs(IrcClient client, IrcMessage ircMessage, ServerInfo serverInfo)
        {
            IrcMessage = ircMessage;
            PrivateMessage = new PrivateMessage(client, IrcMessage, serverInfo);
        }
    }
}
