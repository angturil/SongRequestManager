using System;
using System.Runtime.InteropServices;
using CatCore;
using CatCore.Models.Twitch;
using CatCore.Models.Twitch.IRC;
using CatCore.Services.Interfaces;
using CatCore.Services.Twitch;
using CatCore.Services.Twitch.Interfaces;
using Newtonsoft.Json;



namespace SongRequestManager.ChatHandlers
{
    public class CatCoreHandler : IChatHandler
    {
        bool initialized = false;
            
            
        private object _twitchService;

        public CatCoreHandler()
        {
            if (initialized) return;
            // create chat core instance
            // run twitch services
            try
            {
            var sc = CatCoreInstance.Create();
            _twitchService = ((CatCoreInstance)sc).RunTwitchServices();
            
            ((TwitchService)_twitchService).OnTextMessageReceived += _services_OnTextMessageReceived;
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception was caught when trying to send bot message. {e.ToString()}");
            }

            initialized = true;
        }

        private void _services_OnTextMessageReceived(ITwitchService service, TwitchMessage msg)
        {
            ChatHandler.ParseMessage(ChatUser.FromJSON(JsonConvert.SerializeObject((TwitchUser)msg.Sender)), msg.Message);
        }



        public ChatUser Self => new ChatUser(((ITwitchService) _twitchService).DefaultChannel.Id, ((ITwitchService) _twitchService).DefaultChannel.Name, ((ITwitchService) _twitchService).DefaultChannel.Name, true, false, "#FFFFFF", null, false, false, false);
        
        public bool Connected => ((ITwitchService)_twitchService).DefaultChannel != null;
        
        public void Send(string message, bool isCommand = false)
        {
            if (string.IsNullOrEmpty(message)) return;
            try
            {
                var channel =  ((ITwitchService)_twitchService).DefaultChannel;
               
                channel.SendMessage(message);
                
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception was caught when trying to send bot message. {e.ToString()}");
            }
        }
    }
}