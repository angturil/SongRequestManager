using System;

namespace ChatSharp.Events
{
    /// <summary>
    /// Describes a change to a channel or user mode.
    /// </summary>
    public class ModeChangeEventArgs : EventArgs
    {
        /// <summary>
        /// The target of this change (a channel or user).
        /// </summary>
        /// <value>The target.</value>
        public string Target { get; set; }
        /// <summary>
        /// The user who issued the change.
        /// </summary>
        public IrcUser User { get; set; }
        /// <summary>
        /// The mode change string.
        /// </summary>
        public string Change { get; set; }

        internal ModeChangeEventArgs(string target, IrcUser user, string change)
        {
            Target = target;
            User = user;
            Change = change;
        }
    }
}
