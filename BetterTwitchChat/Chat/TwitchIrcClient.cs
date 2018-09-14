using ChatSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BetterTwitchChat.Sprites;
using BetterTwitchChat.Utils;

namespace BetterTwitchChat.Chat {
    public class ChatMessage {
        public string msg = String.Empty;
        public MessageInfo messageInfo = new MessageInfo();
        public ChatMessage(string msg, MessageInfo messageInfo) {
            this.msg = msg;
            this.messageInfo = messageInfo;
        }
    };

    public class MessageInfo {
        public string badges;
        public string twitchEmotes;
        public string nameColor;
        public string sender;
        public string userID;
        public string messageID;
        public List<EmoteInfo> parsedEmotes = new List<EmoteInfo>();
        public List<BadgeInfo> parsedBadges = new List<BadgeInfo>();
    };

    class TwitchIRCClient {
        public static bool Initialized = false;
        public static bool IsConnected = false;
        public static bool JoinedChannel = false;
        public static DateTime ConnectionTime;
        public static ConcurrentStack<ChatMessage> RenderQueue = new ConcurrentStack<ChatMessage>();

        private static IrcClient client;
        private static ChatHandler _betterTwitchChat;

        private static System.Random _random;

        public static void Initialize(ChatHandler betterTwitchChat) {

            _betterTwitchChat = betterTwitchChat;
            _random = new System.Random(DateTime.Now.Millisecond);
            string username = String.Empty;// Plugin.Instance.Config.TwitchUsername;
            string oauthToken = String.Empty;// Plugin.Instance.Config.TwitchoAuthToken;

            if (username == String.Empty) {
                username = $"justinfan{_random.Next(0, int.MaxValue).ToString()}";
                Plugin.Log($"Using username {username}");
            }

            // Create a connection to our twitch chat and setup event listeners
            client = new IrcClient("irc.chat.twitch.tv", new IrcUser(username, username, oauthToken));
            client.ConnectionComplete += Client_ConnectionComplete;
            client.RawMessageRecieved += Client_RawMessageRecieved;
            client.UserJoinedChannel += Client_UserJoinedChannel;
            client.ConnectAsync();
        }

        private static void Client_UserJoinedChannel(object sender, ChatSharp.Events.ChannelUserEventArgs e) {
            JoinedChannel = true;
        }

        public static void OnConnectionComplete() {
            bool foundCurrentChannel = false;
            if (client.Channels.Count() > 0) {
                List<IrcChannel> removalQueue = new List<IrcChannel>();
                foreach (IrcChannel c in client.Channels) {
                    if (c.Name.Substring(1) != Plugin.Instance.Config.TwitchChannel) {
                        c.Part();
                        removalQueue.Add(c);
                    }
                    else {
                        foundCurrentChannel = true;
                    }
                }
                foreach (IrcChannel c in removalQueue) {
                    client.Channels.Remove(c);
                }
            }
            if (!foundCurrentChannel) {
                ConnectionTime = DateTime.Now;
                JoinedChannel = false;
                client.JoinChannel($"#{Plugin.Instance.Config.TwitchChannel}");
                _betterTwitchChat.displayStatusMessage = true;
            }
        }

        private static void Client_ConnectionComplete(object sender, EventArgs e) {
            ConnectionTime = DateTime.Now;
            IsConnected = true;
            client.SendRawMessage("CAP REQ :twitch.tv/tags twitch.tv/commands");
            OnConnectionComplete();
            Plugin.Log("Connected to twitch chat successfully!");
        }

        private static MessageInfo GetMessageInfo(string msgSender, Dictionary<string, string> messageComponents) {
            MessageInfo messageInfo = new MessageInfo();
            messageInfo.twitchEmotes = messageComponents["emotes"];
            messageInfo.badges = messageComponents["badges"];
            messageInfo.userID = messageComponents["user-id"];
            messageInfo.messageID = messageComponents["id"];

            string displayName = messageComponents["display-name"];
            messageInfo.sender = (displayName == null || displayName == string.Empty) ? msgSender : displayName;

            string displayColor = messageComponents["color"];
            messageInfo.nameColor = (displayColor == null || displayColor == string.Empty) ? (String.Format("#{0:X6}", _random.Next(0x1000000)) + "FF") : displayColor;

            if (messageComponents.ContainsKey("bits")) {
                int numBits = Convert.ToInt32(messageComponents["bits"]);
                //Plugin.Instance.Config.TwitchBitBalance += numBits;
                //Plugin.Log($"Got {numBits} bits! Total balance is {Plugin.TwitchBitBalance}");
            }
            return messageInfo;
        }

        private static void Client_RawMessageRecieved(object s, ChatSharp.Events.RawMessageEventArgs e) {
            //Plugin.Log(e.Message);
            try {
                if (e.Message.StartsWith("@")) {
                    string[] parts = e.Message.Split(new char[] { ' ' }, 2);
                    string message = parts[1];
                    Dictionary<string, string> messageComponents = parts[0].Substring(1).Split(';').ToList().ToDictionary(x => x.Substring(0, x.IndexOf('=')), y => y.Substring(y.IndexOf('=') + 1));
                    if (System.Text.RegularExpressions.Regex.IsMatch(message, ":.*!.*@.*.tmi.twitch.tv")) {
                        string msgSender = message.Substring(1, message.IndexOf('!') - 1);
                        string msgPrefix = $":{msgSender}!{msgSender}@{msgSender}.tmi.twitch.tv ";
                        if (message.StartsWith(msgPrefix)) {
                            List<string> msgArray = message.Replace(msgPrefix, "").Split(new char[] { ' ' }, 3).ToList();
                            switch (msgArray[0]) {
                                case "PRIVMSG":
                                    // Grab the info we care about from the current message
                                    MessageInfo messageInfo = GetMessageInfo(msgSender, messageComponents);

                                    // Remove the : from the beginning of the msg
                                    msgArray[2] = msgArray[2].Substring(1);

                                    // Parse any emotes in the message, download them, then queue it for rendering
                                    SpriteParser.Parse(new ChatMessage(Utilities.StripHTML(msgArray[2]), messageInfo), _betterTwitchChat);
                                    break;
                            }
                        }
                    }
                    else {
                        if (message.Contains("ROOMSTATE")) {
                            Plugin.TwitchChannelID = messageComponents["room-id"];
                            Plugin.Log($"Channel room-id: {Plugin.TwitchChannelID}");
                        }
                        else if (message.Contains("CLEARCHAT")) {
                            _betterTwitchChat.OnUserTimedOut(messageComponents["target-user-id"]);

                        }
                        else if (message.Contains("USERNOTICE")) {
                            switch (messageComponents["msg-id"]) {
                                case "sub":
                                case "resub":
                                case "subgift":
                                    MessageInfo messageInfo = GetMessageInfo(String.Empty, messageComponents);
                                    string newMsg = messageComponents["system-msg"].Replace("\\s", " ");
                                    SpriteParser.Parse(new ChatMessage($"<b>{newMsg.Substring(newMsg.IndexOf(" ") + 1)}</b>", messageInfo), _betterTwitchChat);
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                //Plugin.Log($"Caught exception \"{ex.Message}\" from {ex.Source}");
                //Plugin.Log($"Stack trace: {ex.StackTrace}");
            }
        }
    };
}
