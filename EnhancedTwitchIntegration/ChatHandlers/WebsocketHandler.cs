using System;
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
                for (int i=0;i<RequestBotConfig.Instance.WebsocketConnectionAttempts;i++)
                {
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
                }
            return false;
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