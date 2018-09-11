using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace ChatSharp
{
    /// <summary>
    /// A user connected to IRC.
    /// </summary>
    public class IrcUser : IEquatable<IrcUser>
    {
        internal IrcUser()
        {
            Channels = new ChannelCollection();
            ChannelModes = new Dictionary<IrcChannel, List<char?>>();
            Account = "*";
        }

        /// <summary>
        /// Constructs an IrcUser given a hostmask or nick.
        /// </summary>
        public IrcUser(string host) : this()
        {
            if (!host.Contains("@") && !host.Contains("!"))
                Nick = host;
            else
            {
                string[] mask = host.Split('@', '!');
                Nick = mask[0];
                User = mask[1];
                if (mask.Length <= 2)
                {
                    Hostname = "";
                }
                else
                {
                    Hostname = mask[2];
                }
            }
        }

        /// <summary>
        /// Constructs an IrcUser given a nick and user.
        /// </summary>
        public IrcUser(string nick, string user) : this()
        {
            Nick = nick;
            User = user;
            RealName = User;
            Mode = string.Empty;
        }

        /// <summary>
        /// Constructs an IRC user given a nick, user, and password.
        /// </summary>
        public IrcUser(string nick, string user, string password) : this(nick, user)
        {
            Password = password;
        }

        /// <summary>
        /// Constructs an IRC user given a nick, user, password, and real name.
        /// </summary>
        public IrcUser(string nick, string user, string password, string realName) : this(nick, user, password)
        {
            RealName = realName;
        }

        /// <summary>
        /// The user's nick.
        /// </summary>
        public string Nick { get; internal set; }
        /// <summary>
        /// The user's user (an IRC construct, a string that identifies your username).
        /// </summary>
        public string User { get; internal set; }
        /// <summary>
        /// The user's password. Will not be set on anyone but your own user.
        /// </summary>
        public string Password { get; internal set; }
        /// <summary>
        /// The user's mode.
        /// </summary>
        /// <value>The mode.</value>
        public string Mode { get; internal set; }
        /// <summary>
        /// The user's real name.
        /// </summary>
        /// <value>The name of the real.</value>
        public string RealName { get; internal set; }
        /// <summary>
        /// The user's hostname.
        /// </summary>
        public string Hostname { get; internal set; }
        /// <summary>
        /// Channels this user is present in. Note that this only includes channels you are
        /// also present in, even after a successful WHOIS.
        /// </summary>
        /// <value>The channels.</value>
        public ChannelCollection Channels { get; set; }
        /// <summary>
        /// The user's account. If 0 or *, the user is not logged in.
        /// Otherwise, the user is logged in with services.
        /// </summary>
        public string Account { get; set; }

        public string Color = String.Empty;

        internal Dictionary<IrcChannel, List<char?>> ChannelModes { get; set; }

        /// <summary>
        /// This user's hostmask (nick!user@host).
        /// </summary>
        public string Hostmask
        {
            get
            {
                return Nick + "!" + User + "@" + Hostname;
            }
        }

        /// <summary>
        /// Returns true if the user matches the given mask. Can be used to check if a ban applies
        /// to this user, for example.
        /// </summary>
        public bool Match(string mask)
        {
            if (mask.Contains("!") && mask.Contains("@"))
            {
                if (mask.Contains('$'))
                    mask = mask.Remove(mask.IndexOf('$')); // Extra fluff on some networks
                var parts = mask.Split('!', '@');
                if (Match(parts[0], Nick) && Match(parts[1], User) && Match(parts[2], Hostname))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if the given hostmask matches the given mask.
        /// </summary>
        public static bool Match(string mask, string value)
        {
            if (value == null)
                value = string.Empty;
            int i = 0;
            int j = 0;
            for (; j < value.Length && i < mask.Length; j++)
            {
                if (mask[i] == '?')
                    i++;
                else if (mask[i] == '*')
                {
                    i++;
                    if (i >= mask.Length)
                        return true;
                    while (++j < value.Length && value[j] != mask[i]) ;
                    if (j-- == value.Length)
                        return false;
                }
                else
                {
                    if (char.ToUpper(mask[i]) != char.ToUpper(value[j]))
                        return false;
                    i++;
                }
            }
            return i == mask.Length && j == value.Length;
        }

        /// <summary>
        /// True if this user is equal to another (compares hostmasks).
        /// </summary>
        public bool Equals(IrcUser other)
        {
            return other.Hostmask == Hostmask;
        }

        /// <summary>
        /// True if this user is equal to another (compares hostmasks).
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is IrcUser)
                return Equals((IrcUser)obj);
            return false;
        }

        /// <summary>
        /// Returns the hash code of the user's hostmask.
        /// </summary>
        public override int GetHashCode()
        {
            return Hostmask.GetHashCode();
        }

        /// <summary>
        /// Returns the user's hostmask.
        /// </summary>
        public override string ToString()
        {
            return Hostmask;
        }
    }
}
