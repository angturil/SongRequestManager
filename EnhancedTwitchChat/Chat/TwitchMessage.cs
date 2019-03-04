using SimpleJSON;
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
        public string displayName = "";
        public string id = "";
        public string color = "";
        public string badges = "";
        public bool isBroadcaster;
        public bool isMod;
        public bool isSub;
        public bool isTurbo;
        public bool isVip;
        public TwitchUser(string username = "")
        {
            displayName = username;
        }
        public JSONObject ToJson()
        {
            JSONObject obj = new JSONObject();
            obj.Add("displayName", new JSONString(displayName));
            obj.Add("id", new JSONString(id));
            obj.Add("color", new JSONString(color));
            obj.Add("badges", new JSONString(badges));
            obj.Add("isBroadcaster", new JSONBool(isBroadcaster));
            obj.Add("isMod", new JSONBool(isMod));
            obj.Add("isSub", new JSONBool(isSub));
            obj.Add("isTurbo", new JSONBool(isTurbo));
            obj.Add("isVip", new JSONBool(isVip));
            return obj;
        }

        public void FromJson(JSONObject obj)
        {
            displayName = obj["displayName"].Value;
            id = obj["id"].Value;
            color = obj["color"].Value;
            badges = obj["badges"].Value;
            isBroadcaster = obj["isBroadcaster"].AsBool;
            isMod = obj["isMod"].AsBool;
            isSub = obj["isSub"].AsBool;
            isTurbo = obj["isTurbo"].AsBool;
            isVip = obj["isVip"].AsBool;
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
