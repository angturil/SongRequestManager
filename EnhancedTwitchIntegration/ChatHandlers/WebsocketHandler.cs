using WebSocketSharp;
using Newtonsoft.Json;
using ChatCore.Models.Twitch;

namespace SongRequestManager.ChatHandlers
{
    public class WebsocketHandler : IChatHandler
    {
        private static WebSocket _ws;
        public WebsocketHandler()
        {
            _ws = new WebSocket("ws://127.0.0.1:9090/SRM");
            _ws.OnMessage += _ws_OnTextMessageReceived;

            _ws.OnClose += (sender, args) =>
            {
                _ws.Connect();
            };
            _ws.Connect();
        }
        private void _ws_OnTextMessageReceived(object s, MessageEventArgs e)
        {
                
            Plugin.Log("recv: " + e.Data);
            ChatMessage msg = JsonConvert.DeserializeObject<ChatMessage>(e.Data);
            switch(msg.Command )
            { 
                case 's':
                    Self = msg.GetTwitchUser();
                    break;
                case 'c': default:
                    ChatHandler.ParseMessage(msg);
                    break;
            }
        }

        public bool Connected => _ws.IsAlive;
        public TwitchUser Self { get; set; }

        public void Send(string message, bool isCommand = false)
        {
            _ws.Send(message);
        }
    }
}