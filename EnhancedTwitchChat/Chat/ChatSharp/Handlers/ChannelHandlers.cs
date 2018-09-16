using ChatSharp.Events;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ChatSharp.Handlers
{
    internal static class ChannelHandlers
    {
        public static void HandleJoin(IrcClient client, IrcMessage message)
        {
            var user = client.Users.GetOrAdd(message.Prefix);
            var channel = client.Channels.GetOrAdd(message.Parameters[0]);

            if (channel != null)
            {
                if (!user.Channels.Contains(channel))
                    user.Channels.Add(channel);

                // account-notify capability
                if (client.Capabilities.IsEnabled("account-notify"))
                    client.Who(user.Nick, WhoxFlag.None, WhoxField.Nick | WhoxField.AccountName, (whoQuery) =>
                    {
                        if (whoQuery.Count == 1)
                            user.Account = whoQuery[0].User.Account;
                    });

                client.OnUserJoinedChannel(new ChannelUserEventArgs(channel, user));
            }
        }

        public static void HandleGetTopic(IrcClient client, IrcMessage message)
        {
            var channel = client.Channels.GetOrAdd(message.Parameters[1]);
            var old = channel._Topic;
            channel._Topic = message.Parameters[2];
            client.OnChannelTopicReceived(new ChannelTopicEventArgs(channel, old, channel._Topic));
        }

        public static void HandleGetEmptyTopic(IrcClient client, IrcMessage message)
        {
            var channel = client.Channels.GetOrAdd(message.Parameters[1]);
            var old = channel._Topic;
            channel._Topic = message.Parameters[2];
            client.OnChannelTopicReceived(new ChannelTopicEventArgs(channel, old, channel._Topic));
        }

        public static void HandlePart(IrcClient client, IrcMessage message)
        {
            if (!client.Channels.Contains(message.Parameters[0]))
                return; // we aren't in this channel, ignore

            var user = client.Users.Get(message.Prefix);
            var channel = client.Channels[message.Parameters[0]];

            if (user.Channels.Contains(channel))
                user.Channels.Remove(channel);
            if (user.ChannelModes.ContainsKey(channel))
                user.ChannelModes.Remove(channel);

            client.OnUserPartedChannel(new ChannelUserEventArgs(channel, user));
        }

        public static void HandleUserListPart(IrcClient client, IrcMessage message)
        {
            if (client.Capabilities.IsEnabled("userhost-in-names"))
            {
                var channel = client.Channels[message.Parameters[2]];
                var users = message.Parameters[3].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var hostmask in users)
                {
                    if (string.IsNullOrWhiteSpace(hostmask))
                        continue;

                    // Parse hostmask
                    var nick = hostmask.Substring(0, hostmask.IndexOf("!"));
                    var ident = hostmask.Substring(nick.Length + 1, hostmask.LastIndexOf("@") - (nick.Length + 1));
                    var hostname = hostmask.Substring(hostmask.LastIndexOf("@") + 1);

                    // Get user modes
                    var modes = client.ServerInfo.GetModesForNick(nick);
                    if (modes.Count > 0)
                        nick = nick.Remove(0, modes.Count);

                    var user = client.Users.GetOrAdd(nick);
                    if (user.Hostname != hostname && user.User != ident)
                    {
                        user.Hostname = hostname;
                        user.User = ident;
                    }

                    if (!user.Channels.Contains(channel))
                        user.Channels.Add(channel);
                    if (!user.ChannelModes.ContainsKey(channel))
                        user.ChannelModes.Add(channel, modes);
                    else
                        user.ChannelModes[channel] = modes;
                }
            }
            else
            {
                var channel = client.Channels[message.Parameters[2]];
                var users = message.Parameters[3].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var rawNick in users)
                {
                    if (string.IsNullOrWhiteSpace(rawNick))
                        continue;

                    var nick = rawNick;
                    var modes = client.ServerInfo.GetModesForNick(nick);

                    if (modes.Count > 0)
                        nick = rawNick.Remove(0, modes.Count);

                    var user = client.Users.GetOrAdd(nick);

                    if (!user.Channels.Contains(channel))
                        user.Channels.Add(channel);
                    if (!user.ChannelModes.ContainsKey(channel))
                        user.ChannelModes.Add(channel, modes);
                    else
                        user.ChannelModes[channel] = modes;
                }
            }
        }

        public static void HandleUserListEnd(IrcClient client, IrcMessage message)
        {
            var channel = client.Channels[message.Parameters[1]];
            client.OnChannelListRecieved(new ChannelEventArgs(channel));
            if (client.Settings.ModeOnJoin)
            {
                try
                {
                    client.GetMode(channel.Name, c => { /* no-op */ });
                }
                catch { /* who cares */ }
            }
            if (client.Settings.WhoIsOnJoin)
            {
                Task.Factory.StartNew(() => WhoIsChannel(channel, client, 0));
            }
        }

        private static void WhoIsChannel(IrcChannel channel, IrcClient client, int index)
        {
            // Note: joins and parts that happen during this will cause strange behavior here
            Thread.Sleep(client.Settings.JoinWhoIsDelay * 1000);
            var user = channel.Users[index];
            client.WhoIs(user.Nick, (whois) =>
                {
                    user.User = whois.User.User;
                    user.Hostname = whois.User.Hostname;
                    user.RealName = whois.User.RealName;
                    Task.Factory.StartNew(() => WhoIsChannel(channel, client, index + 1));
                });
        }

        public static void HandleKick(IrcClient client, IrcMessage message)
        {
            var channel = client.Channels[message.Parameters[0]];
            var kicked = channel.Users[message.Parameters[1]];
            if (kicked.Channels.Contains(channel))
                kicked.Channels.Remove(channel);
            client.OnUserKicked(new KickEventArgs(channel, new IrcUser(message.Prefix),
                kicked, message.Parameters[2]));
        }
    }
}
