using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using EnhancedTwitchChat.Sprites;
using EnhancedTwitchChat.Utils;
using AsyncTwitch;
using UnityEngine;
using Random = System.Random;
using System.Threading;

namespace EnhancedTwitchChat.Chat {
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
        public static DateTime ConnectionTime;
        public static ConcurrentStack<ChatMessage> RenderQueue = new ConcurrentStack<ChatMessage>();
        
        private static ChatHandler _chatHandler;
        private static Dictionary<int, string> _userColors = new Dictionary<int, string>();

        private static System.Random _random;
        private static string _lastRoomId;
        

        public static void Initialize(ChatHandler chatHandler) {
            _chatHandler = chatHandler;
            _random = new System.Random(DateTime.Now.Millisecond);

            if (!Utilities.IsModInstalled("Asynchronous Twitch Library"))
            {
                Plugin.Log("AsyncTwitch not installed!");
                _chatHandler.displayStatusMessage = true;
                return;
            }

            while (TwitchConnection.Instance == null) Thread.Sleep(50);

            TwitchConnection.Instance.StartConnection();

            while (!TwitchConnection.IsConnected) Thread.Sleep(50);

            Thread.Sleep(1000);

            TwitchConnection.Instance.RegisterOnMessageReceived(TwitchConnection_OnMessageReceived);
            TwitchConnection.Instance.RegisterOnChannelJoined(TwitchConnection_OnChannelJoined);
            TwitchConnection.Instance.RegisterOnChannelParted(TwitchConnection_OnChannelParted);
            TwitchConnection.Instance.RegisterOnRoomStateChanged(TwitchConnection_OnRoomstateChanged);



            TwitchConnection.Instance.JoinRoom(Plugin.Instance.Config.TwitchChannel);

            ConnectionTime = DateTime.Now;
            _chatHandler.displayStatusMessage = true;
            Initialized = true;

            Plugin.Log("AsyncTwitch initialized!");
        }

        private static void TwitchConnection_OnRoomstateChanged(TwitchConnection obj, RoomState roomstate)
        {
            Plugin.Log($"RoomState changed! {roomstate.RoomID}");
            if (roomstate != TwitchConnection.Instance.RoomStates[Plugin.Instance.Config.TwitchChannel]) return;

            if (roomstate.RoomID != _lastRoomId)
            {
                _chatHandler.displayStatusMessage = true;
                _lastRoomId = roomstate.RoomID;
                Plugin.TwitchChannelID = roomstate.RoomID;
                Plugin.Log($"Twitch channel ID is {Plugin.TwitchChannelID}");
            }
        }

        private static void TwitchConnection_OnChannelJoined(TwitchConnection obj, string channel)
        {
            Plugin.Log("Joined channel " + channel);
        }

        private static void TwitchConnection_OnChannelParted(TwitchConnection obj, string channel)
        {
            Plugin.Log("Left channel " + channel);
        }

        private static void TwitchConnection_OnMessageReceived(TwitchConnection twitchCon, TwitchMessage twitchMessage)
        {
            //Plugin.Log(twitchMessage.RawMessage);

            if (twitchMessage.Room != null && twitchMessage.Room.RoomID != Plugin.TwitchChannelID) return;

            try
            {
                if (twitchMessage.RawMessage.StartsWith("@"))
                {
                    string[] parts = twitchMessage.RawMessage.Split(new char[] { ' ' }, 2);
                    string message = parts[1];
                    Dictionary<string, string> messageComponents = parts[0].Substring(1).Split(';').ToList().ToDictionary(x => x.Substring(0, x.IndexOf('=')), y => y.Substring(y.IndexOf('=') + 1));
                    
                    if (System.Text.RegularExpressions.Regex.IsMatch(message, ":.*!.*@.*.tmi.twitch.tv"))
                    {
                        string msgSender = message.Substring(1, message.IndexOf('!') - 1);
                        string msgPrefix = $":{msgSender}!{msgSender}@{msgSender}.tmi.twitch.tv ";
                        if (message.StartsWith(msgPrefix))
                        {
                            List<string> msgArray = message.Replace(msgPrefix, "").Split(new char[] { ' ' }, 3).ToList();
                            switch (msgArray[0])
                            {
                                case "PRIVMSG":
                                    // Grab the info we care about from the current message
                                    MessageInfo messageInfo = GetMessageInfo(twitchMessage, msgSender, messageComponents);

                                    // Remove the : from the beginning of the msg
                                    msgArray[2] = msgArray[2].Substring(1);

                                    // Parse any emotes in the message, download them, then queue it for rendering
                                    SpriteParser.Parse(new ChatMessage(Utilities.StripHTML(msgArray[2]), messageInfo), _chatHandler);
                                    break;
                            }
                        }
                    }
                    else
                    {
                        if (message.Contains("ROOMSTATE"))
                        {
                            Plugin.TwitchChannelID = messageComponents["room-id"];
                            Plugin.Log($"Channel room-id: {Plugin.TwitchChannelID}");
                        }
                        else if (message.Contains("CLEARCHAT"))
                        {
                            _chatHandler.OnUserTimedOut(messageComponents["target-user-id"]);

                        }
                        else if (message.Contains("USERNOTICE"))
                        {
                            switch (messageComponents["msg-id"])
                            {
                                case "sub":
                                case "resub":
                                case "subgift":
                                    MessageInfo messageInfo = GetMessageInfo(twitchMessage, String.Empty, messageComponents);
                                    string newMsg = messageComponents["system-msg"].Replace("\\s", " ");
                                    SpriteParser.Parse(new ChatMessage($"<b>{newMsg.Substring(newMsg.IndexOf(" ") + 1)}</b>", messageInfo), _chatHandler);
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log($"Caught exception \"{ex.Message}\" from {ex.Source}");
                Plugin.Log($"Stack trace: {ex.StackTrace}");
            }
        }
        
        
        private static MessageInfo GetMessageInfo(TwitchMessage twitchMessage, string msgSender, Dictionary<string, string> messageComponents) {
            MessageInfo messageInfo = new MessageInfo();
            messageInfo.twitchEmotes = messageComponents["emotes"];
            messageInfo.badges = messageComponents["badges"];
            messageInfo.userID = messageComponents["user-id"];
            messageInfo.messageID = messageComponents["id"];

            string displayName = messageComponents["display-name"];
            messageInfo.sender = (displayName == null || displayName == string.Empty) ? msgSender : displayName;

            string displayColor = messageComponents["color"];
            if ((displayColor == null || displayColor == String.Empty) && !_userColors.ContainsKey(msgSender.GetHashCode())) {
                _userColors.Add(msgSender.GetHashCode(), (String.Format("#{0:X6}", new Random(msgSender.GetHashCode()).Next(0x1000000)) + "FF"));
            }
            messageInfo.nameColor = (displayColor == null || displayColor == string.Empty) ? _userColors[msgSender.GetHashCode()] : displayColor;

            if (messageComponents.ContainsKey("bits")) {
                int numBits = Convert.ToInt32(messageComponents["bits"]);
                //Plugin.Instance.Config.TwitchBitBalance += numBits;
                //Plugin.Log($"Got {numBits} bits! Total balance is {Plugin.TwitchBitBalance}");
            }
            return messageInfo;
        }
    };
}
