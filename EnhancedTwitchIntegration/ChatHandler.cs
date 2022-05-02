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
        private static BeatsaberRequestUIHandler _bsruiHandler;
        private bool ChatCorePluginPresent;
        private bool CatCorePluginPresent;
        private bool BSPlusPluginPresent;
        private static ChatUser _defaultSelf;
        private static List<string> CensorList = new List<string>();

        public void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }



        public void Init()
        {
            if (initialized) return;
            ChatCorePluginPresent = IPA.Loader.PluginManager.GetPlugin("ChatCore") != null;
            CatCorePluginPresent = IPA.Loader.PluginManager.GetPlugin("CatCore") != null;
            BSPlusPluginPresent = IPA.Loader.PluginManager.GetPlugin("BeatSaberPlus_ChatIntegrations") != null;
            
            Plugin.Log($"Chatcore is installed? {ChatCorePluginPresent}");
            
            if(CatCorePluginPresent && !RequestBotConfig.Instance.DisableChatcore)
                _chatHandlers.Add(new CatCoreHandler());
            if(ChatCorePluginPresent && !CatCorePluginPresent && !RequestBotConfig.Instance.DisableChatcore)
                _chatHandlers.Add(new ChatCoreHandler());
            if (BSPlusPluginPresent && !ChatCorePluginPresent && !CatCorePluginPresent &&
                !RequestBotConfig.Instance.DisableChatcore)
                _chatHandlers.Add(new BSPlusHandler());
            _wsHandler = new WebsocketHandler();
            _chatHandlers.Add(_wsHandler);
            if (RequestBotConfig.Instance.BeatsaverRequestUIEnabled)
            {
                _bsruiHandler = new BeatsaberRequestUIHandler();
                _chatHandlers.Add(_bsruiHandler);
            }
            // create chat core instance
            _defaultSelf = new ChatUser("0", "SRM", "SRM", true, false, "#FFFFFF", null, false, false, false);
            
            initialized = true;
        }


        public static void ParseMessage(ChatUser sender, string msg, Func<bool> callback = null)
        {
            RequestBot.COMMAND.Parse(sender, msg, 0, "", callback);
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
            if(CensorList.Any(word => message.Contains(word)))
                foreach(var word in CensorList){
                    message = message.Replace(word, "***");
                }
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
        
        public static void BeatsaberRequestUiHandlerConnect()
        {
            if (RequestBotConfig.Instance.BeatsaverRequestUIEnabled)
            {
                _bsruiHandler.ConnectWebsocket();
            }
        }
        
        public static bool WebsocketHandlerConnected()
        {
            return _wsHandler.Connected;
        }
    }
}