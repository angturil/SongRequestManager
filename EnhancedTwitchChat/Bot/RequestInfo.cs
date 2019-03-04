using EnhancedTwitchChat.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnhancedTwitchChat.Bot
{
    public class RequestInfo
    {
        public TwitchUser requestor;
        public string request;
        public bool isBeatSaverId;
        public bool isPersistent = false;
        public DateTime requestTime;
        public RequestInfo(TwitchUser requestor, string request, DateTime requestTime, bool isBeatSaverId)
        {
            this.requestor = requestor;
            this.request = request;
            this.isBeatSaverId = isBeatSaverId;
            this.requestTime = requestTime;
        }
    }
}
