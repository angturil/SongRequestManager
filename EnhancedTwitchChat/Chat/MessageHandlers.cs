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
        private static void ParseRoomstateTag(Match t, string channel)
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

        private static void ParseMessageTag(Match t, ref TwitchMessage twitchMsg)
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
                    twitchMsg.user.isBroadcaster = twitchMsg.user.badges.Contains("broadcaster/");
                    twitchMsg.user.isSub = twitchMsg.user.badges.Contains("subscriber/");
                    twitchMsg.user.isTurbo = twitchMsg.user.badges.Contains("turbo/");
                    twitchMsg.user.isMod = twitchMsg.user.badges.Contains("moderator/");
                    break;
                case "color":
                    twitchMsg.user.color = t.Groups["Value"].Value;
                    break;
                case "display-name":
                    twitchMsg.user.displayName = t.Groups["Value"].Value;
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
            }
        }

        public static void PRIVMSG(TwitchMessage twitchMsg, MatchCollection tags)
        {
            twitchMsg.user.displayName = twitchMsg.hostString.Split('!')[0];
            foreach (Match t in tags)
                ParseMessageTag(t, ref twitchMsg);

            MessageParser.Parse(new ChatMessage(Utilities.StripHTML(twitchMsg.message), twitchMsg));
        #if REQUEST_BOT
            if (Config.Instance.SongRequestBot)
                RequestBot.COMMAND.Parse(twitchMsg.user, twitchMsg.message);
        #endif
        }

        public static void JOIN(TwitchMessage twitchMsg, MatchCollection tags)
        {
            if (!TwitchWebSocketClient.ChannelInfo.ContainsKey(twitchMsg.channelName))
                TwitchWebSocketClient.ChannelInfo.Add(twitchMsg.channelName, new TwitchRoom(twitchMsg.channelName));

            Plugin.Log($"Success joining channel #{twitchMsg.channelName} (RoomID: {twitchMsg.roomId})");
        }

        public static void ROOMSTATE(TwitchMessage twitchMsg, MatchCollection tags)
        {
            foreach (Match t in tags)
                ParseRoomstateTag(t, twitchMsg.channelName);
        }

        public static void USERNOTICE(TwitchMessage twitchMsg, MatchCollection tags)
        {
            foreach (Match t in tags)
                ParseMessageTag(t, ref twitchMsg);

            string msgId = String.Empty, systemMsg = String.Empty;
            foreach (Match t in tags)
            {
                switch (t.Groups["Tag"].Value)
                {
                    case "msg-id":
                        msgId = t.Groups["Value"].Value;
                        break;
                    case "system-msg":
                        systemMsg = t.Groups["Value"].Value.Replace("\\s", " ");
                        break;
                    default:
                        break;
                }
            }
            switch(msgId)
            {
                case "sub":
                case "resub":
                case "subgift":
                case "anonsubgift":
                    MessageParser.Parse(new ChatMessage($"{systemMsg.Substring(systemMsg.IndexOf(" ") + 1).Split(new char[] { '\n' }, 2)[0]}", twitchMsg));
                    if(twitchMsg.message != String.Empty)
                        MessageParser.Parse(new ChatMessage(twitchMsg.message, twitchMsg));
                    break;
                case "raid":
                    break;
                case "ritual":
                    break;
            }
        }

        public static void USERSTATE(TwitchMessage twitchMsg, MatchCollection tags)
        {
            foreach (Match t in tags)
                ParseMessageTag(t, ref twitchMsg);

            TwitchWebSocketClient.OurTwitchUser = twitchMsg.user;

            if (!(twitchMsg.user.isBroadcaster || twitchMsg.user.isMod))
            {
                TwitchMessage tmpMessage = new TwitchMessage();
                tmpMessage.user.displayName = "NOTICE";
                tmpMessage.user.color = "FF0000FF";
                MessageParser.Parse(new ChatMessage($"Twitch account {twitchMsg.user.displayName} is not a moderator of channel #{twitchMsg.channelName}. The default user rate limit is 20 messages per 30 seconds; to increase this limit to 100, grant this user moderator privileges.", tmpMessage));
            }
        }

        public static void CLEARCHAT(TwitchMessage twitchMsg, MatchCollection tags)
        {
            string userId = "!FULLCLEAR!";
            foreach (Match t in tags)
            {
                if (t.Groups["Tag"].Value == "target-user-id")
                {
                    userId = t.Groups["target-user-id"].Value;
                    break;
                }
            }
            ChatHandler.Instance.PurgeMessagesFromUser(userId);
        }

        public static void CLEARMSG(TwitchMessage twitchMsg, MatchCollection tags)
        {
            string msgId = String.Empty;
            foreach (Match t in tags)
            {
                if (t.Groups["Tag"].Value == "target-msg-id")
                {
                    msgId = t.Groups["target-msg-id"].Value;
                    break;
                }
            }
            ChatHandler.Instance.PurgeChatMessageById(msgId);
        }

        public static void MODE(TwitchMessage twitchMsg, MatchCollection tags)
        {
            //Plugin.Log("MODE message received!");
        }
    }
}
