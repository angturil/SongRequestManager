using System;
using System.Collections.Generic;
using System.Linq;
using ChatCore;
using ChatCore.Interfaces;
using ChatCore.Models.Twitch;
using ChatCore.Services.Twitch;
using WebSocketSharp;
using Newtonsoft.Json;
using SongRequestManager.ChatHandlers;

namespace SongRequestManager
{
    public class ChatHandler : PersistentSingleton<ChatHandler>
    {
        bool initialized = false;
        private static List<IChatHandler> _chatHandlers = new List<IChatHandler>();
        private static WebsocketHandler _wsHandler;
        private bool ChatCorePluginPresent;

        public void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }



        public void Init()
        {
            if (initialized) return;
            ChatCorePluginPresent = IPA.Loader.PluginManager.GetPlugin("ChatCore") != null;
            if(ChatCorePluginPresent)
                _chatHandlers.Add(new ChatCoreHandler());
            _wsHandler = new WebsocketHandler();
            _chatHandlers.Add(_wsHandler);
            // create chat core instance

            initialized = true;
        }


        public static void ParseMessage(TwitchUser sender, string msg)
        {
            RequestBot.COMMAND.Parse(sender, msg);
        }
        
        public static void ParseMessage(ChatMessage msg)
        {
            RequestBot.COMMAND.Parse(msg.GetTwitchUser(), msg.Message);
        }



        public static TwitchUser Self => _chatHandlers[0].Self;

        public static bool Connected =>  _chatHandlers.Any(c=> c.Connected == true); 
        
        public static void Send(string message, bool isCommand = false)
        {
            if (string.IsNullOrEmpty(message)) return;
            try
            {
                foreach (var handler in _chatHandlers)
                {
                    handler.Send(message, isCommand);
                }
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception was caught when trying to send bot message. {e.ToString()}");
            }
        }

        public static void WebsocketHandlerConnect()
        {
            _wsHandler.ConnectWebsocket();
        }
    }
}