using System;
using SongRequestManager.SimpleJSON;
using SongRequestManager.ChatHandlers;
using UnityEngine;
using static SongRequestManager.RequestBot;

namespace SongRequestManager
{
    public class SongRequest
    {
        public JSONObject song;
        public ChatUser requestor = new ChatUser();
        public DateTime requestTime;
        public RequestStatus status;
        public string requestInfo; // Contains extra song info, Like : Sub/Donation request, Deck pick, Empty Queue pick,Mapper request, etc.

        public SongRequest() { }
        public SongRequest(JSONObject song, ChatUser requestor, DateTime requestTime, RequestStatus status = RequestStatus.Invalid, string requestInfo = "")
        {
            this.song = song;
            this.requestor = requestor;
            this.status = status;
            this.requestTime = requestTime;
            this.requestInfo = requestInfo;
        }

        public JSONObject ToJson()
        {
            JSONObject obj = new JSONObject();
            obj.Add("status", new JSONString(status.ToString()));
            obj.Add("requestInfo", new JSONString(requestInfo));
            obj.Add("time", new JSONString(requestTime.ToFileTime().ToString()));
            obj.Add("requestor", requestor.ToString());
            obj.Add("song", song);
            return obj;
        }

        public SongRequest FromJson(JSONObject obj)
        {
            requestor = ChatUser.FromJSON(obj["requestor"].Value);
            requestTime = DateTime.FromFileTime(long.Parse(obj["time"].Value));
            status = (RequestStatus)Enum.Parse(typeof(RequestStatus), obj["status"].Value);
            song = obj["song"].AsObject;
            requestInfo = obj["requestInfo"].Value;
            return this;
        }

        public static string GetCensoredData(JSONObject song, string field,  DateTime timeSince)
        {
            //if (RequestBotConfig.Instance.minimumUploadTimeCensor < (timeSince - DateTime.Parse(song["createdAt"].Value)).Minutes) 
            //    return "***";
            return song[field].Value;
        }
    }
}