using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using EnhancedTwitchChat.Sprites;
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
        public static ConcurrentQueue<TwitchMessage> MessageQueue = new ConcurrentQueue<TwitchMessage>();
        public static string ChannelID = string.Empty;

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
            while (TwitchConnection.Instance == null) Thread.Sleep(50);

            TwitchConnection.Instance.StartConnection();

            while (!TwitchConnection.IsConnected) Thread.Sleep(50);

            Thread.Sleep(1000);

            TwitchConnection.Instance.RegisterOnMessageReceived(TwitchConnection_OnMessageReceived);
            TwitchConnection.Instance.RegisterOnChannelJoined(TwitchConnection_OnChannelJoined);
            TwitchConnection.Instance.RegisterOnChannelParted(TwitchConnection_OnChannelParted);
            TwitchConnection.Instance.RegisterOnRoomStateChanged(TwitchConnection_OnRoomstateChanged);

            TwitchConnection.Instance.JoinRoom(Plugin.Instance.Config.TwitchChannel);

            ChatHandler.Instance.displayStatusMessage = true;
            Initialized = true;
            ConnectionTime = DateTime.Now;

            Plugin.Log("AsyncTwitch initialized!");

            ProcessingThread();
        }

        private static void TwitchConnection_OnRoomstateChanged(TwitchConnection obj, RoomState roomstate)
        {
            Plugin.Log($"RoomState changed! {roomstate.RoomID}");
            if (roomstate != TwitchConnection.Instance.RoomStates[Plugin.Instance.Config.TwitchChannel]) return;

            if (roomstate.RoomID != _lastRoomId)
            {
                ChatHandler.Instance.displayStatusMessage = true;
                _lastRoomId = roomstate.RoomID;
                TwitchIRCClient.ChannelID = roomstate.RoomID;
                Plugin.Log($"Twitch channel ID is {TwitchIRCClient.ChannelID}");
                ConnectionTime = DateTime.Now;
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
            MessageQueue.Enqueue(twitchMessage);
        }

        private static void ProcessingThread()
        {
            while (true)
            {
                if (MessageQueue.Count > 0 && MessageQueue.TryDequeue(out var twitchMessage))
                {
                    if (twitchMessage.Room != null && twitchMessage.Room.RoomID != TwitchIRCClient.ChannelID) return;

                    try
                    {
                        if (twitchMessage.Author != null && twitchMessage.Author.DisplayName != String.Empty)
                            MessageParser.Parse(new ChatMessage(Utilities.StripHTML(twitchMessage.Content), twitchMessage));

                        else
                        {
                            if (twitchMessage.Content.Contains("CLEARCHAT"))
                            {
                                string[] parts = twitchMessage.RawMessage.Split(new char[] { ' ' }, 2);
                                Dictionary<string, string> messageComponents = parts[0].Substring(1).Split(';').ToList().ToDictionary(x => x.Substring(0, x.IndexOf('=')), y => y.Substring(y.IndexOf('=') + 1));
                                ChatHandler.Instance.PurgeChatMessages(messageComponents["target-user-id"]);
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
                    catch (Exception ex)
                    {
                        Plugin.Log($"Caught exception \"{ex.Message}\" from {ex.Source}");
                        Plugin.Log($"Stack trace: {ex.StackTrace}");
                    }
                }
                Thread.Sleep(15);
            }
        }
    };
}
