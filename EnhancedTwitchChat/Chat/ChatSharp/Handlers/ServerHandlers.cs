using ChatSharp.Events;
using System.Linq;

namespace ChatSharp.Handlers
{
    internal static class ServerHandlers
    {
        public static void HandleISupport(IrcClient client, IrcMessage message)
        {
            if (client.ServerInfo == null)
                client.ServerInfo = new ServerInfo();
            foreach (var item in message.Parameters)
            {
                string key, value;
                if (!item.Contains("="))
                {
                    key = item;
                    value = null;
                }
                else
                {
                    key = item.Remove(item.IndexOf('='));
                    value = item.Substring(item.IndexOf('=') + 1);
                }
                // TODO: Consider doing this differently
                // TODO: Allow users to specify other things to handle
                if (!string.IsNullOrEmpty(value))
                {
                    switch (key.ToUpper())
                    {
                        case "PREFIX":
                            var modes = value.Substring(1, value.IndexOf(')') - 1);
                            var prefixes = value.Substring(value.IndexOf(')') + 1);
                            client.ServerInfo.Prefixes = new[] {modes, prefixes};
                            break;
                        case "CHANTYPES":
                            client.ServerInfo.ChannelTypes = value.ToCharArray();
                            break;
                        case "CHANMODES":
                            var chanModeGroups = value.Split(',');
                            client.ServerInfo.SupportedChannelModes.ChannelLists = chanModeGroups[0];
                            client.ServerInfo.SupportedChannelModes.ParameterizedSettings = chanModeGroups[1];
                            client.ServerInfo.SupportedChannelModes.OptionallyParameterizedSettings = chanModeGroups[2];
                            client.ServerInfo.SupportedChannelModes.Settings = chanModeGroups[3];
                            break;
                        case "MODES":
                            client.ServerInfo.MaxModesPerCommand = int.Parse(value);
                            break;
                        case "MAXCHANNELS": // TODO: CHANLIMIT
                            client.ServerInfo.MaxChannelsPerUser = int.Parse(value);
                            break;
                        case "NICKLEN":
                            client.ServerInfo.MaxNickLength = int.Parse(value);
                            break;
                        case "MAXLIST":
                            var limits = value.Split(',');
                            client.ServerInfo.ModeListLimits = new ServerInfo.ModeListLimit[limits.Length];
                            for (int i = 0; i < limits.Length; i++)
                            {
                                var limitedModes = limits[i].Remove(limits[i].IndexOf(':'));
                                var limit = int.Parse(limits[i].Substring(limits[i].IndexOf(':') + 1));
                                foreach (var mode in limitedModes)
                                    client.ServerInfo.ModeListLimits[i] = new ServerInfo.ModeListLimit(mode, limit);
                            }
                            break;
                        case "NETWORK":
                            client.ServerInfo.NetworkName = value;
                            break;
                        case "EXCEPTS":
                            client.ServerInfo.SupportsBanExceptions = value[0];
                            break;
                        case "INVEX":
                            client.ServerInfo.SupportsInviteExceptions = value[0];
                            break;
                        case "TOPICLEN":
                            client.ServerInfo.MaxTopicLength = int.Parse(value);
                            break;
                        case "KICKLEN":
                            client.ServerInfo.MaxKickCommentLength = int.Parse(value);
                            break;
                        case "CHANNELLEN":
                            client.ServerInfo.MaxChannelNameLength = int.Parse(value);
                            break;
                        case "AWAYLEN":
                            client.ServerInfo.MaxAwayLength = int.Parse(value);
                            break;
                    }
                }
                else
                {
                    switch (key.ToUpper())
                    {
                        case "WHOX":
                            client.ServerInfo.ExtendedWho = true;
                            break;
                    }
                }
            }
            client.OnServerInfoRecieved(new SupportsEventArgs(client.ServerInfo));
        }

        public static void HandleMyInfo(IrcClient client, IrcMessage message)
        {
            // 004 sendak.freenode.net ircd-seven-1.1.3 DOQRSZaghilopswz CFILMPQbcefgijklmnopqrstvz bkloveqjfI
            // TODO: Figure out how to properly handle this message
            if (client.ServerInfo == null)
                client.ServerInfo = new ServerInfo();
            if (message.Parameters.Length >= 5)
            {
                foreach (var c in message.Parameters[4])
                {
                    if (!client.ServerInfo.SupportedChannelModes.ChannelUserModes.Contains(c))
                        client.ServerInfo.SupportedChannelModes.ChannelUserModes += c.ToString();
                }
            }
        }
    }
}
