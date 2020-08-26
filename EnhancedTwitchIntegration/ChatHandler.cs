using System;
using ChatCore;
using ChatCore.Models.Twitch;
using ChatCore.Services.Twitch;

namespace SongRequestManager
{
    public class ChatHandler : PersistentSingleton<ChatHandler>
    {
        bool initialized = false;

        private static ChatCoreInstance _sc;
        private static TwitchService _twitchService;

        public void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public void Init()
        {
            if (initialized) return;

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

        public static TwitchUser Self => _twitchService.LoggedInUser;

        public static bool Connected => _twitchService.LoggedInUser != null && _twitchService.Channels.Count > 0;

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
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception was caught when trying to send bot message. {e.ToString()}");
            }
        }
    }
}