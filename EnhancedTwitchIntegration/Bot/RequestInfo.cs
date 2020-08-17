using System;
using ChatCore.Models.Twitch;

namespace SongRequestManager
{
    public class RequestInfo
    {
        public TwitchUser requestor;
        public string request;
        public bool isBeatSaverId;
        public DateTime requestTime;
        public RequestBot.CmdFlags flags; // Flags for the song request, include things like silence, bypass checks, etc.
        public string requestInfo; // This field contains additional information about a request. This could include the source of the request ( deck, Subscription bonus request) , comments about why a song was banned, etc.
        public RequestBot.ParseState state;

        public RequestInfo(TwitchUser requestor, string request, DateTime requestTime, bool isBeatSaverId,  RequestBot.ParseState state,RequestBot.CmdFlags flags = 0,string userstring = "")
        {
            this.requestor = requestor;
            this.request = request;
            this.isBeatSaverId = isBeatSaverId;
            this.requestTime = requestTime;
            this.state = state;
            this.flags = flags;
            this.requestInfo = userstring;
        }
    }
}