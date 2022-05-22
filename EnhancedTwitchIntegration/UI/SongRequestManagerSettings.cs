using BeatSaberMarkupLanguage.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManager.UI
{
    public class SongRequestManagerSettings : PersistentSingleton<SongRequestManagerSettings>
    {
        [UIValue("autopick-first-song")]
        public bool AutopickFirstSong
        {
            get => RequestBotConfig.Instance.AutopickFirstSong;
            set => RequestBotConfig.Instance.AutopickFirstSong = value;
        }

        [UIValue("lowest-allowed-rating")]
        public float LowestAllowedRating
        {
            get => RequestBotConfig.Instance.LowestAllowedRating;
            set => RequestBotConfig.Instance.LowestAllowedRating = value;
        }

        [UIValue("maximum-song-length")]
        public int MaximumSongLength
        {
            get => (int) RequestBotConfig.Instance.MaximumSongLength;
            set => RequestBotConfig.Instance.MaximumSongLength = value;
        }

        [UIValue("minimum-njs")]
        public int MinimumNJS
        {
            get => (int) RequestBotConfig.Instance.MinimumNJS;
            set => RequestBotConfig.Instance.MinimumNJS = value;
        }

        [UIValue("automap")]
        public bool Automap
        {
            get => RequestBotConfig.Instance.Automap;
            set => RequestBotConfig.Instance.Automap = value;
        }

        [UIValue("tts-support")]
        public bool TtsSupport
        {
            get => RequestBotConfig.Instance.BotPrefix != "";
            set => RequestBotConfig.Instance.BotPrefix = value ? "! " : "";
        }

        [UIValue("user-request-limit")]
        public int UserRequestLimit
        {
            get => RequestBotConfig.Instance.UserRequestLimit;
            set => RequestBotConfig.Instance.UserRequestLimit = value;
        }

        [UIValue("sub-request-limit")]
        public int SubRequestLimit
        {
            get => RequestBotConfig.Instance.SubRequestLimit;
            set => RequestBotConfig.Instance.SubRequestLimit = value;
        }

        [UIValue("mod-request-limit")]
        public int ModRequestLimit
        {
            get => RequestBotConfig.Instance.ModRequestLimit;
            set => RequestBotConfig.Instance.ModRequestLimit = value;
        }

        [UIValue("vip-bonus-requests")]
        public int VipBonusRequests
        {
            get => RequestBotConfig.Instance.VipBonusRequests;
            set => RequestBotConfig.Instance.VipBonusRequests = value;
        }

        
        [UIValue("request-time-censor")]
        public int MinimumUploadTimeCensor
        {
            get => RequestBotConfig.Instance.MinimumUploadTimeCensor;
            set => RequestBotConfig.Instance.MinimumUploadTimeCensor = value;
        }
            
        [UIValue("websocket-url")]
        public string WebsocketURL {
            get => RequestBotConfig.Instance.WebsocketURL;
            set => RequestBotConfig.Instance.WebsocketURL = value;
        }

        [UIValue("websocket-enable")]
        public bool WebsocketEnabled
        {
            get => RequestBotConfig.Instance.WebsocketEnabled;
            set => RequestBotConfig.Instance.WebsocketEnabled = value;
        }
        
        [UIAction("connect-click")]
        private void ConnectClick()
        {
            ChatHandler.WebsocketHandlerConnect();
            //modal.HandleBlockerButtonClicked();
        }
        
        [UIValue("disable-chatcore")]
        public bool DisableChatcore
        {
            get => RequestBotConfig.Instance.DisableChatcore;
            set => RequestBotConfig.Instance.DisableChatcore = value;
        }
            
        [UIValue("mod-full-rights")]
        public bool ModFullRights
        {
            get => RequestBotConfig.Instance.ModFullRights;
            set => RequestBotConfig.Instance.ModFullRights = value;
        }
        
        [UIValue("requestui-enable")]
        public bool BeatsaverRequestUIEnabled
        {
            get => RequestBotConfig.Instance.BeatsaverRequestUIEnabled;
            set => RequestBotConfig.Instance.BeatsaverRequestUIEnabled = value;
        }
        
        [UIValue("requestui-id")]
        public string BeatsaverRequestUIId {
            get => RequestBotConfig.Instance.BeatsaverRequestUIId;
            set => RequestBotConfig.Instance.BeatsaverRequestUIId = value;
        }
        
        [UIValue("requestui-url")]
        public string BeatsaverRequestUIurl {
            get => RequestBotConfig.Instance.BeatsaverRequestUIurl;
            set => RequestBotConfig.Instance.BeatsaverRequestUIurl = value;
        }
        
        [UIAction("rqui-connect-click")]
        private void RequestUIConnectClick()
        {
            ChatHandler.BeatsaberRequestUiHandlerConnect();
            //modal.HandleBlockerButtonClicked();
        }

    }
}
