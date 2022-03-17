using System;
using ChatCore;
using ChatCore.Models.Twitch;
using ChatCore.Services.Twitch;
using WebSocketSharp;
using Newtonsoft.Json;

namespace SongRequestManager
{
    public class ChatHandler : PersistentSingleton<ChatHandler>
    {
        bool initialized = false;

        private static ChatCoreInstance _sc;
        private static TwitchService _twitchService;
        private static WebSocket _ws;

        public void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public class IWebSocketMessage
        {
            
            public string Id { get;  set; }

            public string UserName { get;  set; }

            public string DisplayName { get;  set; }

            public string Color { get;  set; }

            public bool IsModerator { get;  set; }

            public bool IsBroadcaster { get;  set; }

            public bool IsSubscriber { get;  set; }

            public bool IsTurbo { get;  set; }

            public bool IsVip { get;  set; }

            public ChatCore.Interfaces.IChatBadge[] Badges { get; set; }

            public string Message { get; set; }
        }
        
        public void Init()
        {
            if (initialized) return;
            _ws = new WebSocket("ws://127.0.0.1:9090/SRM");
            _ws.OnMessage += _ws_OnTextMessageReceived;

            _ws.OnClose += (sender, args) =>
            {
                _ws.Connect();
            };
            _ws.Connect();
            // create chat core instance
            _sc = ChatCoreInstance.Create();

            // run twitch services
            _twitchService = _sc.RunTwitchServices();

            _twitchService.OnTextMessageReceived += _services_OnTextMessageReceived;

            initialized = true;
        }

        private void _services_OnTextMessageReceived(ChatCore.Interfaces.IChatService service, ChatCore.Interfaces.IChatMessage msg)
        {
            RequestBot.COMMAND.Parse((TwitchUser)msg.Sender, msg.Message);
        }

        
        private void _ws_OnTextMessageReceived(object s, MessageEventArgs e)
        {
                
            Plugin.Log("recv: " + e.Data);
            IWebSocketMessage msg = JsonConvert.DeserializeObject<IWebSocketMessage>(e.Data);
            var sender = new TwitchUser(e.Data);
            RequestBot.COMMAND.Parse(sender, msg.Message);
        }
        
        public static TwitchUser Self => _twitchService.LoggedInUser;

        public static bool Connected => (_twitchService.LoggedInUser != null && _twitchService.Channels.Count > 0) || _ws.IsAlive;

        public static void Send(string message, bool isCommand = false)
        {
            if (string.IsNullOrEmpty(message)) return;
            try
            {
                foreach (var channel in _twitchService.Channels)
                {
                    if (isCommand)
                    {
                        _twitchService.SendCommand(message.TrimStart('/'), channel.Value.Name);
                    }
                    else
                    {
                        _twitchService.SendTextMessage(message, channel.Value);
                    }
                }
                _ws.Send(message);
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception was caught when trying to send bot message. {e.ToString()}");
            }
        }
    }
}