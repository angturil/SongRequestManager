using System;

namespace ChatSharp.Events
{
    /// <summary>
    /// Event describing an IRC notice.
    /// </summary>
    public class IrcNoticeEventArgs : EventArgs
    {
        /// <summary>
        /// The IRC message that describes this NOTICE.
        /// </summary>
        /// <value>The message.</value>
        public IrcMessage Message { get; set; }
        /// <summary>
        /// The text of the notice.
        /// </summary>
        public string Notice { get { return Message.Parameters[1]; } }
        /// <summary>
        /// The source of the notice (often a user).
        /// </summary>
        /// <value>The source.</value>
        public string Source { get { return Message.Prefix; } }

        internal IrcNoticeEventArgs(IrcMessage message)
        {
            if (message.Parameters.Length != 2)
                throw new IrcProtocolException("NOTICE was delivered in incorrect format");
            Message = message;
        }
    }
}
