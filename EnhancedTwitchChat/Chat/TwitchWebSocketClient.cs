using EnhancedTwitchChat.Bot;
using EnhancedTwitchChat.Textures;
using EnhancedTwitchChat.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace EnhancedTwitchChat.Chat
{
    public class ChatMessage
    {
        public string msg = String.Empty;
        public TwitchMessage twitchMessage = new TwitchMessage();
        public List<EmoteInfo> parsedEmotes = new List<EmoteInfo>();
        public List<BadgeInfo> parsedBadges = new List<BadgeInfo>();
        public bool isActionMessage = false;

        public ChatMessage(string msg, TwitchMessage messageInfo)
        {
            this.msg = msg;
            this.twitchMessage = messageInfo;
        }
    };

    public class TwitchWebSocketClient
    {
        private static readonly Regex _twitchMessageRegex = new Regex(@":(?<HostName>[\S]+) (?<MessageType>[\S]+) #(?<ChannelName>[\S]+)");
        private static readonly Regex _messageRegex = new Regex(@" #[\S]+ :(?<Message>.*)");
        private static readonly Regex _tagRegex = new Regex(@"(?<Tag>[a-z,0-9,-]+)=(?<Value>[^;\s]+)");

        private static Dictionary<string, Action<TwitchMessage, MatchCollection>> _messageHandlers = new Dictionary<string, Action<TwitchMessage, MatchCollection>>();
        private static Random _rand = new Random();
        private static WebSocketSharp.WebSocket _ws;

        public static bool Initialized = false;
        public static ConcurrentQueue<ChatMessage> RenderQueue = new ConcurrentQueue<ChatMessage>();
        public static Dictionary<string, TwitchRoom> ChannelInfo = new Dictionary<string, TwitchRoom>();
        public static DateTime ConnectionTime;
        public static TwitchUser OurTwitchUser = new TwitchUser("Request Bot");

        public static bool IsChannelValid {
            get
            {
                return TwitchWebSocketClient.ChannelInfo.ContainsKey(Config.Instance.TwitchChannelName) && ChannelInfo[Config.Instance.TwitchChannelName].roomId != String.Empty;
            }
        }

        public static void Initialize()
        {
            // Initialize our message handlers
            _messageHandlers.Add("PRIVMSG", MessageHandlers.PRIVMSG);
            _messageHandlers.Add("ROOMSTATE", MessageHandlers.ROOMSTATE);
            _messageHandlers.Add("USERNOTICE", MessageHandlers.USERNOTICE);
            _messageHandlers.Add("CLEARCHAT", MessageHandlers.CLEARCHAT);
            _messageHandlers.Add("CLEARMSG", MessageHandlers.CLEARMSG);
            _messageHandlers.Add("MODE", MessageHandlers.MODE);
            _messageHandlers.Add("JOIN", MessageHandlers.JOIN);

            // TODO HANDLE USERSTATE

            // Create our websocket object and setup the callbacks
            _ws = new WebSocketSharp.WebSocket("wss://irc-ws.chat.twitch.tv:443");
            _ws.OnOpen += (sender, e) =>
            {
                Plugin.Log("Connected! Sending login info!");
                _ws.Send("CAP REQ :twitch.tv/tags twitch.tv/commands twitch.tv/membership");

                string username = Config.Instance.TwitchUsername;
                if (username == String.Empty || Config.Instance.TwitchOAuthToken == String.Empty)
                    username = "justinfan" + _rand.Next(10000, 1000000);
                else
                    _ws.Send($"PASS {Config.Instance.TwitchOAuthToken}");
                _ws.Send($"NICK {username}");

                if(Config.Instance.TwitchChannelName != String.Empty)
                    _ws.Send($"JOIN #{Config.Instance.TwitchChannelName}");

                // Display a message in the chat informing the user whether or not the connection to the channel was successful
                ConnectionTime = DateTime.Now;
                ChatHandler.Instance.displayStatusMessage = true;

                Initialized = true;
            };
            _ws.OnMessage += Ws_OnMessage;
                
            // Then start the connection
            _ws.ConnectAsync();
        }

        public static void SendMessage(string msg, Action<bool> OnCompleted = null)
        {
            if(_ws.IsConnected) 
                _ws.SendAsync(msg, (success) => { OnCompleted?.Invoke(success); });
        }

        public static void JoinChannel(string channel)
        {
            SendMessage($"JOIN #{channel}");
        }

        public static void PartChannel(string channel)
        {
            SendMessage($"PART #{channel}");
        }
        
        private static void Ws_OnMessage(object sender, WebSocketSharp.MessageEventArgs ev)
        {
            if (!ev.IsText) return;

            string rawMessage = ev.Data.TrimEnd();
            if (rawMessage.StartsWith("PING"))
            {
                Plugin.Log("Ping... Pong.");
                _ws.Send("PONG :tmi.twitch.tv");
            }

            var messageType = _twitchMessageRegex.Match(rawMessage);
            if (messageType.Length == 0)
            {
                Plugin.Log($"Unhandled message: {rawMessage}");
                return;
            }

            // Instantiate our twitch message
            TwitchMessage twitchMsg = new TwitchMessage();
            twitchMsg.rawMessage = rawMessage;
            twitchMsg.message = _messageRegex.Match(twitchMsg.rawMessage).Groups["Message"].Value;
            twitchMsg.hostString = messageType.Groups["HostName"].Value;
            twitchMsg.messageType = messageType.Groups["MessageType"].Value;
            twitchMsg.channelName = messageType.Groups["ChannelName"].Value;

            // Find all the message tags
            var tags = _tagRegex.Matches(rawMessage);

            // Call the appropriate handler for this messageType
            if (_messageHandlers.ContainsKey(twitchMsg.messageType))
                _messageHandlers[twitchMsg.messageType]?.Invoke(twitchMsg, tags);
            else
                Plugin.Log($"Unhandled message that met criteria! {rawMessage}");
        }
    }
}
