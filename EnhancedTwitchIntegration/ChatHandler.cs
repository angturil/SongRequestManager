using System;
using System.Collections.Generic;
using System.Linq;
using SongRequestManager.ChatHandlers;

namespace SongRequestManager
{
    public class ChatHandler : PersistentSingleton<ChatHandler>
    {
        bool initialized = false;
        private static List<IChatHandler> _chatHandlers = new List<IChatHandler>();
        private static WebsocketHandler _wsHandler;
        private bool ChatCorePluginPresent;
        private static ChatUser _defaultSelf;

        public void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }



        public void Init()
        {
            if (initialized) return;
            ChatCorePluginPresent = IPA.Loader.PluginManager.GetPlugin("ChatCore") != null;
            
            Plugin.Log($"Chatcore is installed? {ChatCorePluginPresent}");
            
            if(ChatCorePluginPresent && !RequestBotConfig.Instance.DisableChatcore)
                _chatHandlers.Add(new ChatCoreHandler());

            _wsHandler = new WebsocketHandler();
            _chatHandlers.Add(_wsHandler);
            // create chat core instance
            _defaultSelf = new ChatUser("0", "SRM", "SRM", true, false, "#FFFFFF", null, false, false, false);
            initialized = true;
        }


        public static void ParseMessage(ChatUser sender, string msg)
        {
            RequestBot.COMMAND.Parse(sender, msg);
        }
        
        public static void ParseMessage(ChatMessage msg)
        {
            RequestBot.COMMAND.Parse(msg.GetTwitchUser(), msg.Message);
        }


        public static ChatUser Self => _chatHandlers[0].Connected ? _chatHandlers[0].Self : _defaultSelf;

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