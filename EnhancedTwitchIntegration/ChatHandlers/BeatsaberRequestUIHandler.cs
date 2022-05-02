using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using WebSocketSharp;
using Newtonsoft.Json;
using System.Threading.Tasks;
using SongRequestManager.SimpleJSON;

namespace SongRequestManager.ChatHandlers
{
    public class BeatsaberRequestUIHandler : IChatHandler
    {
        private static WebSocket _ws;
        private ChatUser _self;
        private Task _connectTask;
        private string  _url;
        Regex rx = new Regex(@"(?!\()([a-z0-9]*)(?:\)?) (added|requested|open|closed)(?= to queue.$| by \S+ is next.$|.$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        class BeatsaberRequestUIMessage
        {
            public string Event { get;  set; }//": "newRequest",
            public string SRMEvent => Event;//": "newRequest",
            public string Data { get;  set; }//": "25f",
            public string UserId { get;  set; }//: "UqvQa2UoEuGtwmbw2lXkM", // this one is opaque id of my bot, notopaque looks like "U29831480"
            public string UserDisplayname { get;  set; }//": "Name to display",
            public string UserRole { get;  set; }//": "VIP/None/Moderator/Broadcaster",
            public bool IsSubscriber { get;  set; }
            public string Slug { get;  set; } // some random string
            
        }
        
        
        public BeatsaberRequestUIHandler()
        {
            _url = RequestBotConfig.Instance.BeatsaverRequestUIurl + RequestBotConfig.Instance.BeatsaverRequestUIId;
            _ws = new WebSocket(_url);
            _ws.OnMessage += _ws_OnTextMessageReceived;

            _connectTask = new Task(_connectWebsocket);
            _ws.OnClose += (sender, args) =>
            {
                ConnectWebsocket();
            };
            ConnectWebsocket();
        }

        private void _connectWebsocket()
        {
            if (!Connected && RequestBotConfig.Instance.BeatsaverRequestUIEnabled) 
                try
                {
                    _ws.Connect();

                }
                catch (Exception e)
                {
                    Plugin.Log($"Exception was caught when trying to connect the websocket. {e.ToString()}");
                    _ws.Close();
                    _ws = new WebSocket(_url);
                    _ws.OnMessage += _ws_OnTextMessageReceived;

                    _ws.OnClose += (sender, args) =>
                    {
                        ConnectWebsocket();
                    };
                }
        }

        public void ConnectWebsocket()
        {
            if (_connectTask.Status == TaskStatus.Running) return;
            if (_connectTask.Status != TaskStatus.Created) _connectTask = new Task(_connectWebsocket);
            _connectTask.Start();
        }


        private void _ws_OnTextMessageReceived(object s, MessageEventArgs e)
        {
            //Plugin.Log($"incoming WS data: {e.Data}");
            JSONObject reply = new JSONObject();
            Plugin.Log($"RequestUI Request: {e.Data}");
            BeatsaberRequestUIMessage bmsg = JsonConvert.DeserializeObject<BeatsaberRequestUIMessage>(e.Data);
            ChatUser usr = new ChatUser(bmsg.UserId, bmsg.UserDisplayname, bmsg.UserDisplayname,bmsg.UserRole == "Broadcaster",bmsg.UserRole == "Moderator", "#FFFFFF", null,bmsg.IsSubscriber,false,bmsg.UserRole == "VIP" );
            string Event = "";
            string data = "";
            reply.Add("Slug", new JSONString(bmsg.Slug));
            reply.Add("Error", null);
            switch(bmsg.Event )
            { 
                case "Self":
                    _self = usr;
                    reply.Add("Event", new JSONString("SelfReply"));
                    reply.Add("Data", null);
                    Plugin.Log($"Received userdata");
                    break;
                case "List":
                    reply.Add("Event", new JSONString("ListReply"));
                    var arr = new JSONArray();
                    foreach(var request in RequestQueue.Songs)
                        arr.Add(request.song["id"]);
                    reply.Add("Data", arr);
                    break;
                case "Undo":
                    ChatHandler.ParseMessage(usr, "!oops");
                    reply.Add("Event", new JSONString("UndoReply"));
                    reply.Add("Data", null);
                    break;
                case "Pair":
                    reply.Add("Event", new JSONString("PairReply"));
                    reply.Add("Data", null);
                    break;
                case "Request":
                default:
                    AutoResetEvent callback = new AutoResetEvent (false);
                    Func<bool> action = callback.Set;
                    ChatHandler.ParseMessage(usr, "!bsr " + bmsg.Data, action);
                    callback.WaitOne();
                    reply.Add("Event", new JSONString("RequestReply"));
                    reply.Add("Data", new JSONBool(RequestQueue.Songs.Any(r => r.song["id"] == bmsg.Data)));
                    //Plugin.Log($"Received command: {msg.Message}");
                    break;
            }

            Plugin.Log($"RequestUI Reply: {reply.ToString()}");
            _ws.Send(reply.ToString());
            
        }

        public bool Connected => _ws.IsAlive;
        public ChatUser Self => _self;

        public void Send(string message, bool isCommand = false)
        {
            if (!Connected) return;
            try
            {
                MatchCollection matches = rx.Matches(message);
                foreach (Match match in matches)
                {
                    GroupCollection groups = match.Groups;
                    Plugin.Log($"'{groups[0].Value}''{groups[1].Value}'");
                    JSONObject msg = new JSONObject();
                    switch (groups[1].Value)
                    {
                        case "added":
                            msg.Add("Event", new JSONString("QueueAdd"));
                            msg.Add("Data", new JSONString(groups[0].Value));
                            break;
                        case "requested":
                            msg.Add("Event", new JSONString("QueueRemove"));
                            msg.Add("Data", new JSONString(groups[0].Value));
                            break;
                        case "open":
                        case "closed":
                            msg.Add("Event", new JSONString("QueueOpened"));
                            msg.Add("Data", new JSONBool(groups[1].Value == "open"));
                            break;
                        default:
                            msg.Add("Event", new JSONString(""));
                            msg.Add("Data", new JSONString(groups[0].Value));
                            break;
                    }
                    _ws.Send(msg.ToString());
                }

            }
            catch (Exception e)
            {
                Plugin.Log($"Exception was caught when trying to send bot message. {e.ToString()}");
            }
        }
    }
}