//using EnhancedTwitchChat.Bot;
using EnhancedTwitchChat.Config;
using EnhancedTwitchChat.Textures;
using EnhancedTwitchChat.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

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
        private static WebSocket _ws;

        public static bool Initialized = false;
        public static ConcurrentQueue<ChatMessage> RenderQueue = new ConcurrentQueue<ChatMessage>();
        public static Dictionary<string, TwitchRoom> ChannelInfo = new Dictionary<string, TwitchRoom>();
        public static DateTime ConnectionTime;
        public static TwitchUser OurTwitchUser = new TwitchUser("Request Bot");
        public static bool LoggedIn = true;
        
        private static DateTime _sendLimitResetTime = DateTime.Now;
        private static Queue<string> _sendQueue = new Queue<string>();
        
        private static int _reconnectCooldown = 500;
        private static int _fullReconnects = -1;
        private static string _lastChannel = "";

        private static int _messagesSent = 0;
        private static int _sendResetInterval = 30;
        private static int _messageLimit
        {
            get { return (OurTwitchUser.isBroadcaster || OurTwitchUser.isMod) ? 100 : 20; } // Defines how many messages can be sent within _sendResetInterval without causing a global ban on twitch
        }

        public static bool IsChannelValid
        {
            get
            {
                return ChannelInfo.ContainsKey(TwitchLoginConfig.Instance.TwitchChannelName) && ChannelInfo[TwitchLoginConfig.Instance.TwitchChannelName].roomId != String.Empty;
            }
        }
        
        public static void Initialize()
        {
            // Initialize our message handlers
            _messageHandlers.Add("PRIVMSG", MessageHandlers.PRIVMSG);
            _messageHandlers.Add("ROOMSTATE", MessageHandlers.ROOMSTATE);
            _messageHandlers.Add("USERNOTICE", MessageHandlers.USERNOTICE);
            _messageHandlers.Add("USERSTATE", MessageHandlers.USERSTATE);
            _messageHandlers.Add("CLEARCHAT", MessageHandlers.CLEARCHAT);
            _messageHandlers.Add("CLEARMSG", MessageHandlers.CLEARMSG);
            _messageHandlers.Add("MODE", MessageHandlers.MODE);
            _messageHandlers.Add("JOIN", MessageHandlers.JOIN);

            // Stop config updated callback when we haven't switched channels
            _lastChannel = TwitchLoginConfig.Instance.TwitchChannelName;

            TwitchLoginConfig.Instance.ConfigChangedEvent += Instance_ConfigChangedEvent;

            Connect();
        }

        private static void Instance_ConfigChangedEvent(TwitchLoginConfig obj)
        {
            LoggedIn = true;

            if (Initialized)
            {
                if (TwitchLoginConfig.Instance.TwitchChannelName != _lastChannel)
                {
                    if (_lastChannel != String.Empty)
                        TwitchWebSocketClient.PartChannel(_lastChannel);
                    if (TwitchLoginConfig.Instance.TwitchChannelName != String.Empty)
                        TwitchWebSocketClient.JoinChannel(TwitchLoginConfig.Instance.TwitchChannelName);
                    TwitchWebSocketClient.ConnectionTime = DateTime.Now;
                    ChatHandler.Instance.displayStatusMessage = true;
                }
                _lastChannel = TwitchLoginConfig.Instance.TwitchChannelName;
            }
        }

        public static void Shutdown()
        {
            if (Initialized)
            {
                Initialized = false;
                if (_ws.IsConnected)
                    _ws.Close();
            }
        }

        public static void Connect(bool isManualReconnect = false)
        {
            // If they entered invalid login info before, wait here indefinitely until they edit the config manually
            while (!LoggedIn && !Plugin.Instance.IsApplicationExiting)
                Thread.Sleep(500);

            if (Plugin.Instance.IsApplicationExiting)
                return;

            Plugin.Log("Reconnecting!");

            try
            {
                if (_ws != null && _ws.IsConnected)
                {
                    Plugin.Log("Closing existing connnection to Twitch!");
                    _ws.Close();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
            _fullReconnects++;

            try
            {
                // Create our websocket object and setup the callbacks
                using (_ws = new WebSocket("wss://irc-ws.chat.twitch.tv:443"))
                {
                    _ws.OnOpen += (sender, e) =>
                    {
                        // Reset our reconnect cooldown timer
                        _reconnectCooldown = 500;

                        Plugin.Log("Connected to Twitch!");
                        _ws.Send("CAP REQ :twitch.tv/tags twitch.tv/commands twitch.tv/membership");

                        string username = TwitchLoginConfig.Instance.TwitchUsername;
                        if (username == String.Empty || TwitchLoginConfig.Instance.TwitchOAuthToken == String.Empty)
                            username = "justinfan" + _rand.Next(10000, 1000000);
                        else
                            _ws.Send($"PASS {TwitchLoginConfig.Instance.TwitchOAuthToken}");
                        _ws.Send($"NICK {username}");

                        if (TwitchLoginConfig.Instance.TwitchChannelName != String.Empty)
                            _ws.Send($"JOIN #{TwitchLoginConfig.Instance.TwitchChannelName}");

                        // Display a message in the chat informing the user whether or not the connection to the channel was successful
                        ConnectionTime = DateTime.Now;
                        ChatHandler.Instance.displayStatusMessage = true;
                        Initialized = true;
                    };

                    _ws.OnClose += (sender, e) =>
                    {
                        Plugin.Log("Twitch connection terminated.");
                        Initialized = false;
                    };

                    _ws.OnError += (sender, e) =>
                    {
                        Plugin.Log($"An error occured in the twitch connection! Error: {e.Message}, Exception: {e.Exception}");
                        Initialized = false;
                    };

                    _ws.OnMessage += Ws_OnMessage;

                    // Then start the connection
                    _ws.Connect();

                    // Create a new task to reconnect automatically if the connection dies for some unknown reason
                    Task.Run(() =>
                    {
                        try
                        {
                            while (Initialized && _ws.IsConnected)
                            {
                                //Plugin.Log("Connected and alive!");
                                Thread.Sleep(500);
                            }
                        }
                        catch(ThreadAbortException)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log(ex.ToString());
                        }

                        if (!isManualReconnect)
                        {
                            Thread.Sleep(_reconnectCooldown *= 2);
                            Connect();
                        }
                    });
                    ProcessSendQueue(_fullReconnects);
                }
            }
            catch (ThreadAbortException)
            {
                return;
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
                Thread.Sleep(_reconnectCooldown *= 2);
                Connect();
            }
        }

        private static void ProcessSendQueue(int fullReconnects)
        {
            while(!Plugin.Instance.IsApplicationExiting && _fullReconnects == fullReconnects)
            {
                if (LoggedIn && _ws.ReadyState == WebSocketState.Open)
                {
                    if (_sendLimitResetTime < DateTime.Now)
                    {
                        _messagesSent = 0;
                        _sendLimitResetTime = DateTime.Now.AddSeconds(_sendResetInterval);
                    }

                    if (_sendQueue.Count > 0)
                    {
                        if (_messagesSent < _messageLimit)
                        {
                            string msg = _sendQueue.Dequeue();
                            Plugin.Log($"Sending message {msg}");
                            _ws.Send(msg);
                            _messagesSent++;
                        }
                    }
                }
                Thread.Sleep(250);
            }
            Plugin.Log("Exiting!");
        }

        public static void SendMessage(string msg)
        {
            if (LoggedIn && _ws.ReadyState == WebSocketState.Open)
                _sendQueue.Enqueue(msg);
        }

        public static void JoinChannel(string channel)
        {
            if (LoggedIn && _ws.ReadyState == WebSocketState.Open)
                SendMessage($"JOIN #{channel}");
        }

        public static void PartChannel(string channel)
        {
            if (LoggedIn && _ws.ReadyState == WebSocketState.Open)
                SendMessage($"PART #{channel}");
        }
        
        private static void Ws_OnMessage(object sender, MessageEventArgs ev)
        {
            try
            {
                if (!ev.IsText) return;
                
                //Plugin.Log($"RawMsg: {ev.Data}");
                string rawMessage = ev.Data.TrimEnd();
                if (rawMessage.StartsWith("PING"))
                {
                    Plugin.Log("Ping... Pong.");
                    _ws.Send("PONG :tmi.twitch.tv");
                    return;
                }

                var messageType = _twitchMessageRegex.Match(rawMessage);
                if (messageType.Length == 0)
                {
                    if(rawMessage.Contains("NOTICE * :Login authentication failed"))
                    {
                        Plugin.Log($"Invalid Twitch login info! Closing connection!");
                        LoggedIn = false;
                        _ws.Close();
                        return;
                    }

                    Plugin.Log($"Unhandled message: {rawMessage}");
                    return;
                }

                string channelName = messageType.Groups["ChannelName"].Value;
                if (channelName != TwitchLoginConfig.Instance.TwitchChannelName)
                    return;

                // Instantiate our twitch message
                TwitchMessage twitchMsg = new TwitchMessage();
                twitchMsg.rawMessage = rawMessage;
                twitchMsg.message = _messageRegex.Match(twitchMsg.rawMessage).Groups["Message"].Value;
                twitchMsg.hostString = messageType.Groups["HostName"].Value;
                twitchMsg.messageType = messageType.Groups["MessageType"].Value;
                twitchMsg.channelName = channelName;

                // Find all the message tags
                var tags = _tagRegex.Matches(rawMessage);
                
                // Call the appropriate handler for this messageType
                if (_messageHandlers.ContainsKey(twitchMsg.messageType))
                    _messageHandlers[twitchMsg.messageType]?.Invoke(twitchMsg, tags);
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
        }
    }
}
