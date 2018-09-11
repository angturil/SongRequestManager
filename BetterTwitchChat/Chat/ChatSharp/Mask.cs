using System;

namespace ChatSharp
{
    /// <summary>
    /// A mask that can be used to match against a user's hostmask in a channel list,
    /// such as banned users.
    /// </summary>
    public class Mask
    {
        internal Mask(string value, IrcUser creator, DateTime creationTime)
        {
            Value = value;
            Creator = creator;
            CreationTime = creationTime;
        }

        /// <summary>
        /// The user who created this mask.
        /// </summary>
        public IrcUser Creator { get; set; }
        /// <summary>
        /// The time this mask was added to the channel list.
        /// </summary>
        /// <value>The creation time.</value>
        public DateTime CreationTime { get; set; }
        /// <summary>
        /// The mask string.
        /// </summary>
        public string Value { get; set; }
    }
}
