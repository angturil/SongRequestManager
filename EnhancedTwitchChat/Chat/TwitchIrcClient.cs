using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using EnhancedTwitchChat.Textures;
using EnhancedTwitchChat.Utils;
using AsyncTwitch;
using UnityEngine;
using System.Threading;

namespace EnhancedTwitchChat.Chat
{
    public class ChatMessage
    {
        public string msg = String.Empty;
        public TwitchMessage twitchMessage = new TwitchMessage();
        public List<EmoteInfo> parsedEmotes = new List<EmoteInfo>();
        public List<BadgeInfo> parsedBadges = new List<BadgeInfo>();

        public ChatMessage(string msg, TwitchMessage messageInfo)
        {
            this.msg = msg;
            this.twitchMessage = messageInfo;
        }
    };

    class TwitchIRCClient
    {
        public static bool Initialized = false;
        public static DateTime ConnectionTime;
        public static ConcurrentStack<ChatMessage> RenderQueue = new ConcurrentStack<ChatMessage>();
        public static ConcurrentDictionary<string, ConcurrentQueue<TwitchMessage>> MessageQueues = new ConcurrentDictionary<string, ConcurrentQueue<TwitchMessage>>();
        public static Dictionary<string, string> ChannelIds = new Dictionary<string, string>();

        private static System.Random _random;
        private static string _lastRoomId;


        public static void Initialize()
        {
            _random = new System.Random(DateTime.Now.Millisecond);

            if (!Utilities.IsModInstalled("Asynchronous Twitch Library"))
            {
                Plugin.Log("AsyncTwitch not installed!");
                ChatHandler.Instance.displayStatusMessage = true;
                return;
            }

            InitAsyncTwitch();
        }

        private static void InitAsyncTwitch()
        {
            // Wait for the AsyncTwitch instance to be initialized, then start the connection
            while (TwitchConnection.Instance == null) Thread.Sleep(50);
            TwitchConnection.Instance.StartConnection();

            // Wait until the AsyncTwitch socket connection is established, then assume the IRC connection is active a second later
            while (!TwitchConnection.IsConnected) Thread.Sleep(50);
            Thread.Sleep(2000);

            // Register AsyncTwitch callbacks, then join the channel in the EnhancedTwitchChat config
            TwitchConnection.Instance.RegisterOnMessageReceived(TwitchConnection_OnMessageReceived);
            TwitchConnection.Instance.RegisterOnChannelJoined(TwitchConnection_OnChannelJoined);
            TwitchConnection.Instance.RegisterOnChannelParted(TwitchConnection_OnChannelParted);
            TwitchConnection.Instance.RegisterOnRoomStateChanged(TwitchConnection_OnRoomstateChanged);
            TwitchConnection.Instance.JoinRoom(Config.Instance.TwitchChannel);

            // Display a message in the chat informing the user whether or not the connection to the channel was successful
            ChatHandler.Instance.displayStatusMessage = true;
            Initialized = true;
            ConnectionTime = DateTime.Now;

            // Process any messages we receive from AsyncTwitch
            ProcessingThread();

            Plugin.Log("AsyncTwitch initialized!");
        }

        private static void TwitchConnection_OnRoomstateChanged(TwitchConnection obj, RoomState roomstate)
        {
            Plugin.Log($"RoomState changed for channel #{roomstate.ChannelName} (Room ID: {roomstate.RoomID})");
            ChannelIds[roomstate.ChannelName] = roomstate.RoomID;
            if (roomstate.ChannelName != Config.Instance.TwitchChannel) return;

            if (roomstate.RoomID != _lastRoomId)
            {
                ChatHandler.Instance.displayStatusMessage = true;
                _lastRoomId = roomstate.RoomID;
                ConnectionTime = DateTime.Now;
            }
        }

        private static void TwitchConnection_OnChannelJoined(TwitchConnection obj, string channel)
        {
            if (!ChannelIds.ContainsKey(channel))
                ChannelIds[channel] = String.Empty;

            Plugin.Log("Joined channel " + channel);
        }

        private static void TwitchConnection_OnChannelParted(TwitchConnection obj, string channel)
        {
            Plugin.Log("Left channel " + channel);
        }

        private static void TwitchConnection_OnMessageReceived(TwitchConnection twitchCon, TwitchMessage twitchMessage)
        {
            //if (twitchMessage.Room != null && twitchMessage.Room.ChannelName != Config.Instance.TwitchChannel)
            //{
            //    Plugin.Log($"Channel: {twitchMessage.Room.ChannelName}, ConfigChannel: {Config.Instance.TwitchChannel}");
            //    return;
            //}

            if (twitchMessage.Room != null)
            {
                if (!MessageQueues.ContainsKey(Config.Instance.TwitchChannel))
                    MessageQueues[twitchMessage.Room.ChannelName] = new ConcurrentQueue<TwitchMessage>();

                if (MessageQueues[twitchMessage.Room.ChannelName].Count > Config.Instance.MaxMessages)
                {
                    if (MessageQueues[twitchMessage.Room.ChannelName].TryDequeue(out var dump))
                    {
                        //Plugin.Log($"Dumping message id {dump.Id}, Reason: Too many messages in queue.");
                    }
                }
                MessageQueues[twitchMessage.Room.ChannelName].Enqueue(twitchMessage);
                //Plugin.Log("Enqueued!");
            }
            else
            {
                if (!MessageQueues.ContainsKey("NoRoomMessages"))
                    MessageQueues["NoRoomMessages"] = new ConcurrentQueue<TwitchMessage>();

                MessageQueues["NoRoomMessages"].Enqueue(twitchMessage);
            }
        }

        private static void ProcessingThread()
        {
            while (true)
            {
                try {
                    if (ChatHandler.Instance.initialized)
                    {
                        if (MessageQueues.ContainsKey("NoRoomMessages") && MessageQueues["NoRoomMessages"].Count > 0 && MessageQueues["NoRoomMessages"].TryDequeue(out var noRoomMessage))
                        {
                            //Plugin.Log($"NoRoomMessage: {noRoomMessage.RawMessage}");
                        }

                        if (MessageQueues.ContainsKey(Config.Instance.TwitchChannel) && MessageQueues[Config.Instance.TwitchChannel].Count > 0 && MessageQueues[Config.Instance.TwitchChannel].TryDequeue(out var twitchMessage))
                        {
                            if (twitchMessage.Author != null && twitchMessage.Author.DisplayName != String.Empty)
                                MessageParser.Parse(new ChatMessage(Utilities.StripHTML(twitchMessage.Content), twitchMessage));
                            else
                            {
                                if (twitchMessage.RawMessage.Contains("CLEARCHAT"))
                                {
                                    string[] parts = twitchMessage.RawMessage.Split(new char[] { ' ' }, 2);
                                    Dictionary<string, string> messageComponents = parts[0].Substring(1).Split(';').ToList().ToDictionary(x => x.Substring(0, x.IndexOf('=')), y => y.Substring(y.IndexOf('=') + 1));
                                    ChatHandler.Instance.PurgeMessagesFromUser(messageComponents["target-user-id"]);
                                }
                                //else if (message.Contains("USERNOTICE"))
                                //{
                                //    switch (messageComponents["msg-id"])
                                //    {
                                //        case "sub":
                                //        case "resub":
                                //        case "subgift":
                                //            //MessageInfo messageInfo = GetMessageInfo(twitchMessage, String.Empty, messageComponents);
                                //            string newMsg = messageComponents["system-msg"].Replace("\\s", " ");
                                //            SpriteParser.Parse(new ChatMessage($"<b>{newMsg.Substring(newMsg.IndexOf(" ") + 1)}</b>", twitchMessage));
                                //            break;
                                //    }
                                //}
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log($"Caught exception \"{ex.Message}\" from {ex.Source}");
                    Plugin.Log($"Stack trace: {ex.StackTrace}");
                }
                Thread.Sleep(15);
            }
        }
    };
}
