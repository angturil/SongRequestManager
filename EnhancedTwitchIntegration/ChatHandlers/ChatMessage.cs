using ChatCore.Models.Twitch;
using Newtonsoft.Json;

namespace SongRequestManager.ChatHandlers
{
    public class ChatMessage
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


        public char Command { get;  set; }
        public TwitchUser GetTwitchUser()
        {
            return new TwitchUser(JsonConvert.SerializeObject(this));
        }
    }
}