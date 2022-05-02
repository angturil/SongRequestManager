using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace SongRequestManager.ChatHandlers
{
    public class ChatUser {
        public string Id { get;  set; }

        public string UserName { get;  set; }

        public string DisplayName { get;  set; }

        public string Color { get;  set; }

        public bool IsModerator { get;  set; }

        public bool IsBroadcaster { get;  set; }

        public bool IsSubscriber { get;  set; }

        public bool IsTurbo { get;  set; }

        public bool IsVip { get;  set; }

        public List<KeyValuePair<string, string>> Badges = new List<KeyValuePair<string, string>>();
        
        public bool IsUsingSlashMe;
        
        public Color roleColor => this.IsModerator ? UnityEngine.Color.green : this.IsVip ? UnityEngine.Color.magenta : UnityEngine.Color.white;
        public string roleColorStr => this.IsModerator ? "green" : this.IsVip ? "#E005B9" : "white";

        public ChatUser()
        {
            
        }
        
        
        public ChatUser(string id, string username, string displayName, bool isBroadcaster, bool isMod, string color, List<KeyValuePair<string, string>> badges, bool isSub, bool isTurbo, bool isVip) {

            this.Id = id;
            this.UserName = username;
            this.DisplayName = displayName;
            this.IsBroadcaster = isBroadcaster;
            this.IsModerator = isMod;

            this.Color = color;
            this.IsSubscriber = isSub;
            this.IsTurbo = isTurbo;

            this.IsVip = isVip;
            this.Badges = badges;

            this.IsUsingSlashMe = false;
            
        }

        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }
        

        public static ChatUser FromJSON(string json) {
            return JsonConvert.DeserializeObject<ChatUser>(json);
        }
    }
}
