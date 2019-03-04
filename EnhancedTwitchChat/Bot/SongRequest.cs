using EnhancedTwitchChat.Chat;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static EnhancedTwitchChat.Bot.RequestBot;

namespace EnhancedTwitchChat.Bot
{
    public class SongRequest
    {
        public JSONObject song;
        public TwitchUser requestor = new TwitchUser();
        public DateTime requestTime;
        public RequestStatus status;
        public SongRequest(JSONObject song, TwitchUser requestor, DateTime requestTime, RequestStatus status = RequestStatus.Invalid)
        {
            this.song = song;
            this.requestor = requestor;
            this.status = status;
            this.requestTime = requestTime;
        }

        public JSONObject ToJson()
        {
            JSONObject obj = new JSONObject();
            obj.Add("status", new JSONString(status.ToString()));
            obj.Add("time", new JSONString(requestTime.ToFileTime().ToString()));
            obj.Add("requestor", requestor.ToJson());
            obj.Add("song", song);
            return obj;
        }

        public void FromJson(JSONObject obj)
        {
            requestor.FromJson(obj["requestor"].AsObject);
            requestTime = DateTime.FromFileTime(long.Parse(obj["time"].Value));
            status = (RequestStatus)Enum.Parse(typeof(RequestStatus), obj["status"].Value);
            song = obj["song"].AsObject;
        }
    }
}
