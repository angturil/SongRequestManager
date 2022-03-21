using System;
using System.Runtime.InteropServices;
using ChatCore;
using ChatCore.Models.Twitch;
using ChatCore.Services.Twitch;
using Newtonsoft.Json;



namespace SongRequestManager.ChatHandlers
{
    public class ChatCoreHandler : IChatHandler
    {
        bool initialized = false;
            
            
        private object _twitchService;

        public ChatCoreHandler()
        {
            if (initialized) return;
            // create chat core instance
            // run twitch services
            try
            {
            var sc = ChatCoreInstance.Create();
            _twitchService = ((ChatCoreInstance)sc).RunTwitchServices();
            
            ((TwitchService)_twitchService).OnTextMessageReceived += _services_OnTextMessageReceived;
            
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception was caught when trying to send bot message. {e.ToString()}");
            }

            initialized = true;
        }

        private void _services_OnTextMessageReceived(ChatCore.Interfaces.IChatService service, ChatCore.Interfaces.IChatMessage msg)
        {
            ChatHandler.ParseMessage(ChatUser.FromJSON(JsonConvert.SerializeObject((TwitchUser)msg.Sender)), msg.Message);
        }

        
        
        public ChatUser Self => ChatUser.FromJSON(JsonConvert.SerializeObject(((TwitchService)_twitchService).LoggedInUser));
        
        public bool Connected => ((TwitchService)_twitchService).LoggedInUser != null && ((TwitchService)_twitchService).Channels.Count > 0;
        
        public void Send(string message, bool isCommand = false)
        {
            if (string.IsNullOrEmpty(message)) return;
            try
            {
                foreach (var channel in  ((TwitchService)_twitchService).Channels)
                {
                    if (isCommand)
                    {
                        ((TwitchService)_twitchService).SendCommand(message.TrimStart('/'), channel.Value.Name);
                    }
                    else
                    {
                        ((TwitchService)_twitchService).SendTextMessage(message, channel.Value);
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