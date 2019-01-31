using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnhancedTwitchChat.Chat
{
    public class TwitchEmote
    {
        public string name;
    }

    public class TwitchBadge
    {
        public string name;
        public string version;
    }

    public class TwitchRoom
    {
        public string name = "";
        public string roomId = "";
        public string lang = "";
        public bool emoteOnly;
        public bool followersOnly;
        public bool subsOnly;
        public bool r9k;
        public bool rituals;
        public bool slow;
        public TwitchRoom(string channel)
        {
            name = channel;
        }
    }

    public class TwitchUser
    {
        bool isValid = false;
        public string displayName = "";
        public string id = "";
        public string color = "";
        public string badges = "";
        public bool isBroadcaster;
        public bool isMod;
        public bool isSub;
        public bool isTurbo;
        public TwitchUser(string username = "")
        {
            displayName = username;
        }
    }

    public class TwitchMessage
    {
        public string message = "";
        public string rawMessage = "";
        public string hostString = "";
        public string messageType = "";
        public string channelName = "";
        public string roomId = "";
        public string id = "";
        public string emotes = "";
        public int bits;
        public TwitchUser user = new TwitchUser();
    }
}
