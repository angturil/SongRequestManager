using EnhancedTwitchChat.Bot;
using EnhancedTwitchChat.Textures;
using EnhancedTwitchChat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EnhancedTwitchChat.Chat
{
    class MessageHandlers
    {
        private static void ParseRoomstateTags(Match t, string channel)
        {
            if (!TwitchWebSocketClient.ChannelInfo.ContainsKey(channel))
                TwitchWebSocketClient.ChannelInfo.Add(channel, new TwitchRoom(channel));

            switch (t.Groups["Tag"].Value)
            {
                case "broadcaster-lang":
                    TwitchWebSocketClient.ChannelInfo[channel].lang = t.Groups["Value"].Value;
                    break;
                case "emote-only":
                    TwitchWebSocketClient.ChannelInfo[channel].emoteOnly = t.Groups["Value"].Value == "1";
                    break;
                case "followers-only":
                    TwitchWebSocketClient.ChannelInfo[channel].followersOnly = t.Groups["Value"].Value == "1";
                    break;
                case "r9k":
                    TwitchWebSocketClient.ChannelInfo[channel].r9k = t.Groups["Value"].Value == "1";
                    break;
                case "rituals":
                    TwitchWebSocketClient.ChannelInfo[channel].rituals = t.Groups["Value"].Value == "1";
                    break;
                case "room-id":
                    TwitchWebSocketClient.ChannelInfo[channel].roomId = t.Groups["Value"].Value;
                    break;
                case "slow":
                    TwitchWebSocketClient.ChannelInfo[channel].slow = t.Groups["Value"].Value == "1";
                    break;
                case "subs-only":
                    TwitchWebSocketClient.ChannelInfo[channel].subsOnly = t.Groups["Value"].Value == "1";
                    break;
            }
        }

        private static void ParseMessageTags(Match t, ref TwitchMessage twitchMsg)
        {
            switch (t.Groups["Tag"].Value)
            {
                case "id":
                    twitchMsg.id = t.Groups["Value"].Value;
                    break;
                case "emotes":
                    twitchMsg.emotes = t.Groups["Value"].Value;
                    break;
                case "badges":
                    twitchMsg.user.badges = t.Groups["Value"].Value;
                    if (twitchMsg.user.badges.Contains("broadcaster"))
                        twitchMsg.user.isBroadcaster = true;
                    break;
                case "color":
                    twitchMsg.user.color = t.Groups["Value"].Value;
                    break;
                case "display-name":
                    twitchMsg.user.displayName = t.Groups["Value"].Value;
                    break;
                case "mod":
                    twitchMsg.user.isMod = t.Groups["Value"].Value == "1";
                    break;
                case "subscriber":
                    twitchMsg.user.isSub = t.Groups["Value"].Value == "1";
                    break;
                case "turbo":
                    twitchMsg.user.isTurbo = t.Groups["Value"].Value == "1";
                    break;
                case "user-id":
                    twitchMsg.user.id = t.Groups["Value"].Value;
                    break;
                case "bits":
                    twitchMsg.bits = int.Parse(t.Groups["Value"].Value);
                    break;
                    //case "flags":
                    //    twitchMsg.user.flags = t.Groups["Value"].Value;
                    //    break;
                    //case "emotes-only":
                    //    twitchMsg.emotesOnly = t.Groups["Value"].Value == "1";
                    //    break;
                    //case "user-type":
                    //    twitchMsg.user.type = t.Groups["Value"].Value;
                    //    break;
            }
        }

        public static void PRIVMSG(TwitchMessage twitchMsg, MatchCollection tags)
        {
            twitchMsg.user.displayName = twitchMsg.hostString.Split('!')[0];
            foreach (Match t in tags)
                ParseMessageTags(t, ref twitchMsg);

            MessageParser.Parse(new ChatMessage(Utilities.StripHTML(twitchMsg.message), twitchMsg));
            if (Config.Instance.SongRequestBot)
                RequestBot.Parse(twitchMsg.user, twitchMsg.message);
            //Plugin.Log($"Raw PRIVMSG: {twitchMsg.rawMessage}");
            //Plugin.Log($"{twitchMsg.user.displayName}: {_messageRegex.Match(twitchMsg.rawMessage).Groups["Message"].Value}");
        }

        public static void JOIN(TwitchMessage twitchMsg, MatchCollection tags)
        {
            if (!TwitchWebSocketClient.ChannelInfo.ContainsKey(twitchMsg.channelName))
                TwitchWebSocketClient.ChannelInfo.Add(twitchMsg.channelName, new TwitchRoom(twitchMsg.channelName));

            Plugin.Log($"Success joining channel #{twitchMsg.channelName}");
        }

        public static void ROOMSTATE(TwitchMessage twitchMsg, MatchCollection tags)
        {
            foreach (Match t in tags)
                ParseRoomstateTags(t, twitchMsg.channelName);

            Plugin.Log("ROOMSTATE message received!");
        }

        public static void USERNOTICE(TwitchMessage twitchMsg, MatchCollection tags)
        {
            Plugin.Log("USERNOTICE message received!");
        }

        public static void CLEARCHAT(TwitchMessage twitchMsg, MatchCollection tags)
        {
            string userId = String.Empty;
            foreach (Match t in tags)
            {
                if (t.Name == "target-user-id")
                {
                    userId = t.Groups["target-user-id"].Value;
                    break;
                }
            }
            ChatHandler.Instance.PurgeMessagesFromUser(userId); //target-user-id
        }

        public static void CLEARMSG(TwitchMessage twitchMsg, MatchCollection tags)
        {
            string msgId = String.Empty;
            foreach (Match t in tags)
            {
                if (t.Name == "target-msg-id")
                {
                    msgId = t.Groups["target-msg-id"].Value;
                    break;
                }
            }
            ChatHandler.Instance.PurgeChatMessageById(msgId);
        }

        public static void MODE(TwitchMessage twitchMsg, MatchCollection tags)
        {
            Plugin.Log("MODE message received!");
        }
    }
}
