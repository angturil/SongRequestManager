using System;
using System.Collections.Generic;
using System.Net;
namespace ChatSharp
{
    /// <summary>
    /// The results of an IRC WHO (WHOX protocol) query. Depending on what information you request,
    /// some of these fields may be null.
    /// </summary>
    public class ExtendedWho
    {
        internal ExtendedWho()
        {
            QueryType = -1;
            Channel = "*";
            User = new IrcUser();
            IP = string.Empty;
            Server = string.Empty;
            Flags = string.Empty;
            Hops = -1;
            TimeIdle = -1;
            OpLevel = "n/a";
        }

        /// <summary>
        /// Type of the query. Defaults to a randomly generated number so ChatSharp can keep
        /// track of WHOX queries it issues.
        /// </summary>
        public int QueryType { get; internal set; }
        /// <summary>
        /// Channel name
        /// </summary>
        public string Channel { get; internal set; }
        /// <summary>
        /// User
        /// </summary>
        public IrcUser User { get; internal set; }
        /// <summary>
        /// Numeric IP address of the user (unresolved hostname)
        /// </summary>
        public string IP { get; internal set; }
        /// <summary>
        /// Server name
        /// </summary>
        public string Server { get; internal set; }
        /// <summary>
        /// User flags
        /// </summary>
        public string Flags { get; internal set; }
        /// <summary>
        /// Distance, in hops
        /// </summary>
        public int Hops { get; internal set; }
        /// <summary>
        /// Time the user has been idle for
        /// </summary>
        public int TimeIdle { get; internal set; }
        /// <summary>
        /// OP level of the user in the channel
        /// </summary>
        public string OpLevel { get; internal set; }
    }

    /// <summary>
    /// Field matching flags for WHOX protocol.
    /// </summary>
    [Flags]
    public enum WhoxFlag
    {
        /// <summary>
        /// Do not match any flag at all. By doing so, ircds defaults to 'nuhsr'
        /// (everything except the numeric IP).
        /// </summary>
        None = 0,
        /// <summary>
        /// Matches nick (in nick!user@host)
        /// </summary>
        Nick = 1,
        /// <summary>
        /// Matches username (in nick!user@host)
        /// </summary>
        Username = 2,
        /// <summary>
        /// Matches hostname (in nick!user@host)
        /// </summary>
        Hostname = 4,
        /// <summary>
        /// Matches numeric IPs
        /// </summary>
        NumericIp = 8,
        /// <summary>
        /// Matches server name
        /// </summary>
        ServerName = 16,
        /// <summary>
        /// Matches informational text
        /// </summary>
        Info = 32,
        /// <summary>
        /// Matches account name
        /// </summary>
        AccountName = 64,
        /// <summary>
        /// Matches visible and invisble users in a channel
        /// </summary>
        DelayedChanMembers = 128,
        /// <summary>
        /// Matches IRC operators
        /// </summary>
        IrcOp = 256,
        /// <summary>
        /// Special purpose flag, normally only IRC ops have access to it.
        /// </summary>
        Special = 512,
        /// <summary>
        /// Matches all of the flags defined.
        /// </summary>
        All = ~0
    }

    /// <summary>
    /// Information fields for WHOX protocol.
    /// </summary>
    [Flags]
    public enum WhoxField
    {
        /// <summary>
        /// Do not include any field at all.
        /// By doing so, ircds defaults to sending a normal WHO reply.
        /// </summary>
        None = 0,
        /// <summary>
        /// Includes the querytype in the reply
        /// </summary>
        QueryType = 1,
        /// <summary>
        /// Includes the first channel name
        /// </summary>
        Channel = 2,
        /// <summary>
        /// Includes the userID (username)
        /// </summary>
        Username = 4,
        /// <summary>
        /// Includes the IP
        /// </summary>
        UserIp = 8,
        /// <summary>
        /// Includes the user's hostname
        /// </summary>
        Hostname = 16,
        /// <summary>
        /// Includes the server name
        /// </summary>
        ServerName = 32,
        /// <summary>
        /// Includes the user's nick
        /// </summary>
        Nick = 64,
        /// <summary>
        /// Includes all flags a user has
        /// </summary>
        Flags = 128,
        /// <summary>
        /// Includes the "distance" in hops
        /// </summary>
        Hops = 256,
        /// <summary>
        /// Includes the idle time (0 for remote users)
        /// </summary>
        TimeIdle = 512,
        /// <summary>
        /// Includes the user's account name
        /// </summary>
        AccountName = 1024,
        /// <summary>
        /// Includes the user's op level in the channel
        /// </summary>
        OpLevel = 2048,
        /// <summary>
        /// Includes the user's real name
        /// </summary>
        RealName = 4096,
        /// <summary>
        /// Includes all fields defined
        /// </summary>
        All = ~0
    }

    internal static class WhoxEnumExtensions
    {
        public static string AsString(this WhoxFlag flag)
        {
            // nuhisradox
            var result = string.Empty;
            if ((flag & WhoxFlag.Nick) != 0)
                result += 'n';
            if ((flag & WhoxFlag.Username) != 0)
                result += 'u';
            if ((flag & WhoxFlag.Hostname) != 0)
                result += 'h';
            if ((flag & WhoxFlag.NumericIp) != 0)
                result += 'i';
            if ((flag & WhoxFlag.ServerName) != 0)
                result += 's';
            if ((flag & WhoxFlag.Info) != 0)
                result += 'r';
            if ((flag & WhoxFlag.AccountName) != 0)
                result += 'a';
            if ((flag & WhoxFlag.DelayedChanMembers) != 0)
                result += 'd';
            if ((flag & WhoxFlag.IrcOp) != 0)
                result += 'o';
            if ((flag & WhoxFlag.Special) != 0)
                result += 'x';

            if (flag == WhoxFlag.None)
                result = string.Empty;

            return result;
        }

        public static string AsString(this WhoxField field)
        {
            // cdfhilnrstuao
            var result = string.Empty;

            if ((field & WhoxField.Channel) != 0)
                result += 'c';
            if ((field & WhoxField.Hops) != 0)
                result += 'd';
            if ((field & WhoxField.Flags) != 0)
                result += 'f';
            if ((field & WhoxField.Hostname) != 0)
                result += 'h';
            if ((field & WhoxField.UserIp) != 0)
                result += 'i';
            if ((field & WhoxField.TimeIdle) != 0)
                result += 'l';
            if ((field & WhoxField.Nick) != 0)
                result += 'n';
            if ((field & WhoxField.RealName) != 0)
                result += 'r';
            if ((field & WhoxField.ServerName) != 0)
                result += 's';
            if ((field & WhoxField.QueryType) != 0)
                result += 't';
            if ((field & WhoxField.Username) != 0)
                result += 'u';
            if ((field & WhoxField.AccountName) != 0)
                result += 'a';
            if ((field & WhoxField.OpLevel) != 0)
                result += 'o';

            if (field == WhoxField.None)
                result = string.Empty;

            return result;

        }
    }
}
