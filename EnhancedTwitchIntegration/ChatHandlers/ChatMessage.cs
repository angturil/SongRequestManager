using Newtonsoft.Json;
using System.Collections.Generic;

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

        public List<KeyValuePair<string, string>> Badges { get; set; }
        //public ChatCore.Interfaces.IChatBadge[] Badges { get; set; }

        public string Message { get; set; }


        public char Command { get;  set; }
        public ChatUser GetTwitchUser()
        {
            return ChatUser.FromJSON(JsonConvert.SerializeObject(this));
        }
    }
}