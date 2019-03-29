using StreamCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManager.Config
{
    public class RequestBotConfig
    {
        private string FilePath = Path.Combine(Environment.CurrentDirectory, "UserData", "EnhancedTwitchChat", "RequestBotSettings.ini");

        //public string RequestCommandAliases = "request,bsr,add,sr";

#if !REQUEST_BOT
        public string SongBlacklist = "";
#endif
        
        public bool RequestQueueOpen = true;
        public bool PersistentRequestQueue = true;

        public int RequestHistoryLimit = 40;
        public int UserRequestLimit = 2;
        public int SubRequestLimit = 5;
        public int ModRequestLimit = 10;
        public int VipBonusRequests = 1; // VIP's get bonus requests in addition to their base limit *IMPLEMENTED*
        public int SessionResetAfterXHours = 6; // Number of hours before persistent session properties are reset (ie: Queue, Played , Duplicate List)
        public float LowestAllowedRating = 0; // Lowest allowed song rating to be played 0-100 *IMPLEMENTED*, needs UI
        //public int MaxiumAddScanRange = 40; // How far down the list to scan , currently in use by unpublished commands

        #if UNRELEASED
        public string DeckList = "fun hard challenge dance chill";
        #endif

        public bool AutopickFirstSong = false; // Pick the first song that !bsr finds instead of showing a short list. *IMPLEMENTED*, needs UI
        public bool AllowModAddClosedQueue = true; // Allow moderator to add songs while queue is closed 
        public bool SendNextSongBeingPlayedtoChat = true; // Enable chat message when you hit play
        public bool UpdateQueueStatusFiles = true; // Create and update queue list and open/close status files for OBS *IMPLEMENTED*, needs UI
        public int MaximumQueueTextEntries = 8;

        public string BotPrefix = "";

        public event Action<RequestBotConfig> ConfigChangedEvent;

        private readonly FileSystemWatcher _configWatcher;
        private bool _saving;

        private static RequestBotConfig _instance = null;
        public static RequestBotConfig Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new RequestBotConfig();
                return _instance;
            }

            private set
            {
                _instance = value;
            }
        }

        public RequestBotConfig()
        {
            Instance = this;

            if (!Directory.Exists(Path.GetDirectoryName(FilePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

            if (File.Exists(FilePath))
            {
                Load();
            }
            Save();

            _configWatcher = new FileSystemWatcher(Path.GetDirectoryName(FilePath))
            {
                NotifyFilter = NotifyFilters.LastWrite,
                Filter = "RequestBotSettings.ini",
                EnableRaisingEvents = true
            };
            _configWatcher.Changed += ConfigWatcherOnChanged;
        }

        ~RequestBotConfig()
        {
            _configWatcher.Changed -= ConfigWatcherOnChanged;
        }

        public void Load()
        {
            ConfigSerializer.LoadConfig(this, FilePath);
        }

        public void Save(bool callback = false)
        {
            if (!callback)
                _saving = true;
            ConfigSerializer.SaveConfig(this, FilePath);
        }

        private void ConfigWatcherOnChanged(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            if (_saving)
            {
                _saving = false;
                return;
            }

            Load();

            if (ConfigChangedEvent != null)
            {
                ConfigChangedEvent(this);
            }
        }
    }
}
