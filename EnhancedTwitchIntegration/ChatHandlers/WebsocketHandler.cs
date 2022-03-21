using System;
using WebSocketSharp;
using Newtonsoft.Json;

namespace SongRequestManager.ChatHandlers
{
    public class WebsocketHandler : IChatHandler
    {
        private static WebSocket _ws;
        private ChatUser _self;
        public WebsocketHandler()
        {
            _ws = new WebSocket(RequestBotConfig.Instance.WebsocketURL);
            _ws.OnMessage += _ws_OnTextMessageReceived;

            _ws.OnClose += (sender, args) =>
            {
                ConnectWebsocket();
            };
            ConnectWebsocket();
        }

        public bool ConnectWebsocket()
        {
            if (!Connected) 
                try
                {
                    _ws.Connect();
                }
                catch (Exception e)
                {
                    Plugin.Log($"Exception was caught when trying to send bot message. {e.ToString()}");
                    _ws.Close();
                    _ws = new WebSocket(RequestBotConfig.Instance.WebsocketURL);
                    _ws.OnMessage += _ws_OnTextMessageReceived;

                    _ws.OnClose += (sender, args) =>
                    {
                        ConnectWebsocket(); 
                    };
                }
            if (Connected) return true;
                
            return false;
        }
        private void _ws_OnTextMessageReceived(object s, MessageEventArgs e)
        {
            Plugin.Log($"incoming WS data: {e.Data}");
            ChatMessage msg = JsonConvert.DeserializeObject<ChatMessage>(e.Data);
            switch(msg.Command )
            { 
                case 's':
                    _self = msg.GetTwitchUser();
                    Plugin.Log($"Received userdata");
                    break;
                case 'c': default:
                    Plugin.Log($"Received command: {msg.Message}");
                    ChatHandler.ParseMessage(msg.GetTwitchUser(), msg.Message);
                    break;
            }
        }

        public bool Connected => _ws.IsAlive;
        public ChatUser Self => _self;

        public void Send(string message, bool isCommand = false)
        {
            if (!Connected) return;
            try
            {
                _ws.Send(message);
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception was caught when trying to send bot message. {e.ToString()}");
            }
        }
    }
}