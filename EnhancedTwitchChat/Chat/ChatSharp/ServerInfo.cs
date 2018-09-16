using System.Collections.Generic;
using System.Linq;

namespace ChatSharp
{
    /// <summary>
    /// Information provided by the server about its featureset.
    /// </summary>
    public class ServerInfo
    {
        internal ServerInfo()
        {
            // Guess for some defaults
            Prefixes = new[] { "ovhaq", "@+%&~" };
            SupportedChannelModes = new ChannelModes();
            IsGuess = true;
            ExtendedWho = false;
        }

        /// <summary>
        /// Gets the mode for a given channel user list prefix.
        /// </summary>
        public char? GetModeForPrefix(char prefix)
        {
            if (Prefixes[1].IndexOf(prefix) == -1)
                return null;
            return Prefixes[0][Prefixes[1].IndexOf(prefix)];
        }

        /// <summary>
        /// Gets the channel modes for a given user nick.
        /// Returns an empty array if user has no modes.
        /// </summary>
        /// <returns></returns>
        public List<char?> GetModesForNick(string nick)
        {
            var supportedPrefixes = Prefixes[1];
            List<char?> modeList = new List<char?>();
            List<char> nickPrefixes = new List<char>();

            foreach (char prefix in supportedPrefixes)
            {
                if (nick.Contains(prefix))
                {
                    nick.Remove(nick.IndexOf(prefix));
                    if (!nickPrefixes.Contains(prefix))
                    {
                        nickPrefixes.Add(prefix);
                        var mode = GetModeForPrefix(prefix);
                        if (!modeList.Contains(mode))
                            modeList.Add(mode);
                    }
                }
            }

            return modeList;
        }

        /// <summary>
        /// ChatSharp makes some assumptions about what the server supports in order to function properly.
        /// If it has not recieved a 005 message giving it accurate information, this value will be true.
        /// </summary>
        public bool IsGuess { get; internal set; }
        /// <summary>
        /// Nick prefixes for special modes in channel user lists
        /// </summary>
        public string[] Prefixes { get; internal set; }
        /// <summary>
        /// Supported channel prefixes (i.e. '#')
        /// </summary>
        public char[] ChannelTypes { get; internal set; }
        /// <summary>
        /// Channel modes supported by this server
        /// </summary>
        public ChannelModes SupportedChannelModes { get; set; }
        /// <summary>
        /// The maximum number of MODE changes possible with a single command
        /// </summary>
        public int? MaxModesPerCommand { get; set; }
        /// <summary>
        /// The maximum number of channels a user may join
        /// </summary>
        public int? MaxChannelsPerUser { get; set; } // TODO: Support more than just # channels
        /// <summary>
        /// Maximum length of user nicks on this server
        /// </summary>
        public int? MaxNickLength { get; set; }
        /// <summary>
        /// The limits imposed on list modes, such as +b
        /// </summary>
        public ModeListLimit[] ModeListLimits { get; set; }
        /// <summary>
        /// The name of the network, as identified by the server
        /// </summary>
        public string NetworkName { get; set; }
        /// <summary>
        /// Set to ban exception character if this server supports ban exceptions
        /// </summary>
        public char? SupportsBanExceptions { get; set; }
        /// <summary>
        /// Set to invite exception character if this server supports invite exceptions
        /// </summary>
        public char? SupportsInviteExceptions { get; set; }
        /// <summary>
        /// Set to maximum topic length for this server
        /// </summary>
        public int? MaxTopicLength { get; set; }
        /// <summary>
        /// Set to the maximum length of a KICK comment
        /// </summary>
        public int? MaxKickCommentLength { get; set; }
        /// <summary>
        /// Set to the maximum length of a channel name
        /// </summary>
        public int? MaxChannelNameLength { get; set; }
        /// <summary>
        /// Set to the maximum length of an away message
        /// </summary>
        public int? MaxAwayLength { get; set; }
        /// <summary>
        /// Server supports WHOX (WHO extension)
        /// </summary>
        public bool ExtendedWho { get; set; }

        /// <summary>
        /// Modes a server supports that are applicable to channels.
        /// </summary>
        public class ChannelModes
        {
            internal ChannelModes()
            {
                // Guesses
                ChannelLists = "eIbq";
                ParameterizedSettings = "k";
                OptionallyParameterizedSettings = "flj";
                Settings = string.Empty;
                ChannelUserModes = "vhoaq"; // I have no idea what I'm doing here
            }

            /// <summary>
            /// Modes that are used for lists (i.e. bans).
            /// </summary>
            public string ChannelLists { get; internal set; }
            /// <summary>
            /// Modes that can be set on a user of a channel (i.e. ops, voice, etc).
            /// </summary>
            public string ChannelUserModes { get; set; }
            /// <summary>
            /// Modes that take a parameter (i.e. +k).
            /// </summary>
            public string ParameterizedSettings { get; internal set; }
            /// <summary>
            /// Modes that take an optional parameter (i.e. +f).
            /// </summary>
            public string OptionallyParameterizedSettings { get; internal set; }
            /// <summary>
            /// Modes that change channel settings.
            /// </summary>
            public string Settings { get; internal set; }
        }

        /// <summary>
        /// Limits imposed on channel lists, such as the maximum bans per channel.
        /// </summary>
        public class ModeListLimit
        {
            internal ModeListLimit(char mode, int maximum)
            {
                Mode = mode;
                Maximum = maximum;
            }

            /// <summary>
            /// The mode character this applies to (i.e. 'b')
            /// </summary>
            public char Mode { get; internal set; }
            /// <summary>
            /// The maximum entries for this list.
            /// </summary>
            public int Maximum { get; internal set; }
        }
    }
}
