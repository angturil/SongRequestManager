using System;
using System.Collections.Generic;
using System.Linq;

namespace ChatSharp
{
    public partial class IrcClient
    {
        /// <summary>
        /// Changes your nick.
        /// </summary>
        public void Nick(string newNick)
        {
            SendRawMessage("NICK {0}", newNick);
            User.Nick = newNick;
        }

        /// <summary>
        /// Sends a message to one or more destinations (channels or users).
        /// </summary>
        public void SendMessage(string message, params string[] destinations)
        {
            const string illegalCharacters = "\r\n\0";
            if (destinations == null || !destinations.Any()) throw new InvalidOperationException("Message must have at least one target.");
            if (illegalCharacters.Any(message.Contains)) throw new ArgumentException("Illegal characters are present in message.", "message");
            string to = string.Join(",", destinations);
            SendRawMessage("PRIVMSG {0} :{1}{2}", to, PrivmsgPrefix, message);
        }

        /// <summary>
        /// Sends a CTCP action (i.e. "* SirCmpwn waves hello") to one or more destinations.
        /// </summary>
        public void SendAction(string message, params string[] destinations)
        {
            const string illegalCharacters = "\r\n\0";
            if (destinations == null || !destinations.Any()) throw new InvalidOperationException("Message must have at least one target.");
            if (illegalCharacters.Any(message.Contains)) throw new ArgumentException("Illegal characters are present in message.", "message");
            string to = string.Join(",", destinations);
            SendRawMessage("PRIVMSG {0} :\x0001ACTION {1}{2}\x0001", to, PrivmsgPrefix, message);
        }

        /// <summary>
        /// Sends a NOTICE to one or more destinations (channels or users).
        /// </summary>
        public void SendNotice(string message, params string[] destinations)
        {
            const string illegalCharacters = "\r\n\0";
            if (destinations == null || !destinations.Any()) throw new InvalidOperationException("Message must have at least one target.");
            if (illegalCharacters.Any(message.Contains)) throw new ArgumentException("Illegal characters are present in message.", "message");
            string to = string.Join(",", destinations);
            SendRawMessage("NOTICE {0} :{1}{2}", to, PrivmsgPrefix, message);
        }

        /// <summary>
        /// Leaves the specified channel.
        /// </summary>
        public void PartChannel(string channel)
        {
            if (!Channels.Contains(channel))
                throw new InvalidOperationException("Client is not present in channel.");
            SendRawMessage("PART {0}", channel);
        }

        /// <summary>
        /// Leaves the specified channel, giving a reason for your departure.
        /// </summary>
        public void PartChannel(string channel, string reason)
        {
            if (!Channels.Contains(channel))
                throw new InvalidOperationException("Client is not present in channel.");
            SendRawMessage("PART {0} :{1}", channel, reason);
        }

        /// <summary>
        /// Joins the specified channel.
        /// </summary>
        public void JoinChannel(string channel, string key = null)
        {
            if (Channels.Contains(channel))
                throw new InvalidOperationException("Client is already present in channel.");

            string joinCmd = string.Format("JOIN {0}", channel);
            if (!string.IsNullOrEmpty(key))
                joinCmd += string.Format(" {0}", key);

            SendRawMessage(joinCmd, channel);

            // account-notify capability
            var flags = WhoxField.Nick | WhoxField.Hostname | WhoxField.AccountName | WhoxField.Username;

            if (Capabilities.IsEnabled("account-notify"))
                Who(channel, WhoxFlag.None, flags, (whoList) =>
                {
                    if (whoList.Count > 0)
                    {
                        foreach (var whoQuery in whoList)
                        {
                            var user = Users.GetOrAdd(whoQuery.User.Hostmask);
                            user.Account = whoQuery.User.Account;
                        }
                    }
                });
        }

        /// <summary>
        /// Sets the topic for the specified channel.
        /// </summary>
        public void SetTopic(string channel, string topic)
        {
            if (!Channels.Contains(channel))
                throw new InvalidOperationException("Client is not present in channel.");
            SendRawMessage("TOPIC {0} :{1}", channel, topic);
        }

        /// <summary>
        /// Retrieves the topic for the specified channel.
        /// </summary>
        public void GetTopic(string channel)
        {
            SendRawMessage("TOPIC {0}", channel);
        }

        /// <summary>
        /// Kicks the specified user from the specified channel.
        /// </summary>
        public void KickUser(string channel, string user)
        {
            SendRawMessage("KICK {0} {1} :{1}", channel, user);
        }

        /// <summary>
        /// Kicks the specified user from the specified channel.
        /// </summary>
        public void KickUser(string channel, string user, string reason)
        {
            SendRawMessage("KICK {0} {1} :{2}", channel, user, reason);
        }

        /// <summary>
        /// Invites the specified user to the specified channel.
        /// </summary>
        public void InviteUser(string channel, string user)
        {
            SendRawMessage("INVITE {1} {0}", channel, user);
        }

        /// <summary>
        /// Sends a WHOIS query asking for information on the given nick.
        /// </summary>
        public void WhoIs(string nick)
        {
            WhoIs(nick, null);
        }

        /// <summary>
        /// Sends a WHOIS query asking for information on the given nick, and a callback
        /// to run when we have received the response.
        /// </summary>
        public void WhoIs(string nick, Action<WhoIs> callback)
        {
            var whois = new WhoIs();
            RequestManager.QueueOperation("WHOIS " + nick, new RequestOperation(whois, ro =>
                {
                    if (callback != null)
                        callback((WhoIs)ro.State);
                }));
            SendRawMessage("WHOIS {0}", nick);
        }

        /// <summary>
        /// Sends an extended WHO query asking for specific information about a single user
        /// or the users in a channel, and runs a callback when we have received the response.
        /// </summary>
        public void Who(string target, WhoxFlag flags, WhoxField fields, Action<List<ExtendedWho>> callback)
        {
            if (ServerInfo.ExtendedWho)
            {
                var whox = new List<ExtendedWho>();

                // Generate random querytype for WHO query
                int queryType = RandomNumber.Next(0, 999);

                // Add the querytype field if it wasn't defined
                var _fields = fields;
                if ((fields & WhoxField.QueryType) == 0)
                    _fields |= WhoxField.QueryType;

                string whoQuery = string.Format("WHO {0} {1}%{2},{3}", target, flags.AsString(), _fields.AsString(), queryType);
                string queryKey = string.Format("WHO {0} {1} {2:D}", target, queryType, _fields);

                RequestManager.QueueOperation(queryKey, new RequestOperation(whox, ro =>
                {
                    callback?.Invoke((List<ExtendedWho>)ro.State);
                }));
                SendRawMessage(whoQuery);
            }
            else
            {
                var whox = new List<ExtendedWho>();

                string whoQuery = string.Format("WHO {0}", target);

                RequestManager.QueueOperation(whoQuery, new RequestOperation(whox, ro =>
                {
                    callback?.Invoke((List<ExtendedWho>)ro.State);
                }));
                SendRawMessage(whoQuery);
            }
        }

        /// <summary>
        /// Requests the mode of a channel from the server.
        /// </summary>
        public void GetMode(string channel)
        {
            GetMode(channel, null);
        }

        /// <summary>
        /// Requests the mode of a channel from the server, and passes it to a callback later.
        /// </summary>
        public void GetMode(string channel, Action<IrcChannel> callback)
        {
            RequestManager.QueueOperation("MODE " + channel, new RequestOperation(channel, ro =>
                {
                    var c = Channels[(string)ro.State];
                    if (callback != null)
                        callback(c);
                }));
            SendRawMessage("MODE {0}", channel);
        }

        /// <summary>
        /// Sets the mode of a target.
        /// </summary>
        public void ChangeMode(string target, string change)
        {
            SendRawMessage("MODE {0} {1}", target, change);
        }

        /// <summary>
        /// Gets a collection of masks from a channel by a mode. This can be used, for example,
        /// to get a list of bans.
        /// </summary>
        public void GetModeList(string channel, char mode, Action<MaskCollection> callback)
        {
            RequestManager.QueueOperation("GETMODE " + mode + " " + channel, new RequestOperation(new MaskCollection(), ro =>
                {
                    var c = (MaskCollection)ro.State;
                    if (callback != null)
                        callback(c);
                }));
            SendRawMessage("MODE {0} {1}", channel, mode);
        }
    }
}
