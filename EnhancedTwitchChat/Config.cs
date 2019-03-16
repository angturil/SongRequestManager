using EnhancedTwitchChat.Bot;
using IllusionPlugin;
using EnhancedTwitchChat.SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedTwitchChat
{
    public class OldConfigOptions
    {
        public string TwitchChannel = "";
    }

    public class OldBlacklistOption
    {
        public string SongBlacklist;
    }

    public class Config
    {
        public string FilePath { get; }

        public string TwitchChannelName = "";
        public string TwitchUsername = "";
        public string TwitchOAuthToken = "";
        public string FontName = "Segoe UI";
        //public int BombBitValue;
        //public int TwitchBitBalance;

        public float ChatScale = 1.1f;
        public float ChatWidth = 160;
        public float MessageSpacing = 2.0f;
        public int MaxChatLines = 30;

        public float PositionX = 2.0244143f;
        public float PositionY = 0.373768f;
        public float PositionZ = 0.08235432f;

        public float RotationX = 2.026023f;
        public float RotationY = 97.58616f;
        public float RotationZ = 1.190764f;

        public float TextColorR = 1;
        public float TextColorG = 1;
        public float TextColorB = 1;
        public float TextColorA = 1;

        public float BackgroundColorR = 0;
        public float BackgroundColorG = 0;
        public float BackgroundColorB = 0;
        public float BackgroundColorA = 0.6f;
        public float BackgroundPadding = 4;

        public bool LockChatPosition = false;
        public bool ReverseChatOrder = false;
        public bool AnimatedEmotes = true;
        public bool DrawShadows = false;
        public bool SongRequestBot = false;
        public bool PersistentRequestQueue = true;
        public bool FilterCommandMessages = false;
        public bool FilterBroadcasterMessages = false;
        public bool FilterUserlistMessages = true; // Filter messages in chatexclude.users ( pick a better name ) 

        public string RequestCommandAliases = "request,bsr,add,sr";

    #if !REQUEST_BOT
        public string SongBlacklist = "";
    #endif

        public bool QueueOpen = true;

        public int RequestHistoryLimit = 20;
        public int RequestLimit = 5;
        public int SubRequestLimit = 5;
        public int ModRequestLimit = 10;
        public int VipBonus = 1; // VIP's get bonus requests in addition to their base limit *IMPLEMENTED*
        public int RequestCooldownMinutes = 0; // BUG: Currently inactive

        public string DeckList = "fun hard challenge dance";

        public float lowestallowedrating = 0; // Lowest allowed song rating to be played 0-100 *IMPLEMENTED*, needs UI

        public bool AutopickFirstSong = false; // Pick the first song that !bsr finds instead of showing a short list. *IMPLEMENTED*, needs UI

        public bool AllowModAddClosedQueue = true; // Allow moderator to add songs while queue is closed 

        public bool SendNextSongBeingPlayedtoChat = true; // Enable chat message when you hit play

        public bool UpdateQueueStatusFiles = true; // Create and update queue list and open/close status files for OBS *IMPLEMENTED*, needs UI
        public bool ShowStarRating = true; // Show star rating (quality, not difficulty) on songs being requested *IMPLEMENTED*, needs UI

        public int MaxiumAddScanRange = 40; // How far down the list to scan , currently in use by unpublished commands
        public int maxaddnewresults = 5;  // Max results per command,mainly to avoid overwhelming the queue *needs UI*

        public event Action<Config> ConfigChangedEvent;

        private readonly FileSystemWatcher _configWatcher;
        private bool _saving;

        public static Config Instance = null;

        // These settings let you configure the text of various bot commands.  BUG:I'd like to remove it from here for this release


        public int MaximumQueueTextEntries = 8;

        public int SessionResetAfterXHours = 6; // Number of hours before persistent session properties are reset (ie: Queue, Played , Duplicate List)

        public Color TextColor
        {
            get
            {
                return new Color(TextColorR, TextColorG, TextColorB, TextColorA);
            }
            set
            {
                TextColorR = value.r;
                TextColorG = value.g;
                TextColorB = value.b;
                TextColorA = value.a;
            }
        }

        public Color BackgroundColor
        {
            get
            {
                return new Color(BackgroundColorR, BackgroundColorG, BackgroundColorB, BackgroundColorA);
            }
            set
            {
                BackgroundColorR = value.r;
                BackgroundColorG = value.g;
                BackgroundColorB = value.b;
                BackgroundColorA = value.a;
            }
        }

        public Vector3 ChatPosition
        {
            get
            {
                return new Vector3(PositionX, PositionY, PositionZ);
            }
            set
            {
                PositionX = value.x;
                PositionY = value.y;
                PositionZ = value.z;
            }
        }

        public Vector3 ChatRotation
        {
            get { return new Vector3(RotationX, RotationY, RotationZ); }
            set
            {
                RotationX = value.x;
                RotationY = value.y;
                RotationZ = value.z;
            }
        }

        public Config(string filePath)
        {
            Instance = this;
            FilePath = filePath;

            if (!Directory.Exists(Path.GetDirectoryName(FilePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

            string oldFilePath = Path.Combine(Environment.CurrentDirectory, "UserData", "EnhancedTwitchChat.ini");
            if(File.Exists(oldFilePath) && !File.Exists(filePath))
            {
                File.Move(oldFilePath, filePath);
            }

            if (File.Exists(FilePath))
            {
                Load();

                var text = File.ReadAllText(FilePath);
                if (text.Contains("TwitchChannel="))
                {
                    var oldConfig = new OldConfigOptions();
                    ConfigSerializer.LoadConfig(oldConfig, FilePath);

                    TwitchChannelName = oldConfig.TwitchChannel;
                }

#if REQUEST_BOT
                if (text.Contains("SongBlacklist=")) 
                {
                    var oldConfig = new OldBlacklistOption();
                    ConfigSerializer.LoadConfig(oldConfig, FilePath);

                    if(oldConfig.SongBlacklist.Length > 0)
                        SongBlacklist.ConvertFromList(oldConfig.SongBlacklist.Split(','));
                }
#endif
            }
            CorrectConfigSettings();
            Save();

            _configWatcher = new FileSystemWatcher(Path.GetDirectoryName(filePath))
            {
                NotifyFilter = NotifyFilters.LastWrite,
                Filter = "EnhancedTwitchChat.ini",
                EnableRaisingEvents = true
            };
            _configWatcher.Changed += ConfigWatcherOnChanged;
        }

        ~Config()
        {
            _configWatcher.Changed -= ConfigWatcherOnChanged;
        }

        public void Load()
        {
            ConfigSerializer.LoadConfig(this, FilePath);

            CorrectConfigSettings();
        }

        public void Save(bool callback = false)
        {
            if (!callback)
                _saving = true;
            ConfigSerializer.SaveConfig(this, FilePath);
        }

        private void ImportAsyncTwitchConfig()
        {
            try
            {
                string asyncTwitchConfig = Path.Combine(Environment.CurrentDirectory, "UserData", "AsyncTwitchConfig.json");
                if (File.Exists(asyncTwitchConfig))
                {
                    JSONNode node = JSON.Parse(File.ReadAllText(asyncTwitchConfig));
                    if (!node.IsNull)
                    {
                        if (node["Username"].IsString && TwitchUsername == String.Empty)
                            TwitchUsername = node["Username"].Value;
                        if (node["ChannelName"].IsString && TwitchChannelName == String.Empty)
                            TwitchChannelName = node["ChannelName"].Value;
                        if (node["OauthKey"].IsString && TwitchOAuthToken == String.Empty)
                            TwitchOAuthToken = node["OauthKey"].Value;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log($"Error when trying to parse AsyncTwitchConfig! {e}");
            }
        }

        private void CorrectConfigSettings()
        {
            if (BackgroundPadding < 0)
                BackgroundPadding = 0;
            if (MaxChatLines < 1)
                MaxChatLines = 1;

            ImportAsyncTwitchConfig();

            if (TwitchOAuthToken != String.Empty && !TwitchOAuthToken.StartsWith("oauth:"))
                TwitchOAuthToken = "oauth:" + TwitchOAuthToken;

            if (TwitchChannelName.Length > 0)
            {
                if (TwitchChannelName.Contains("/"))
                {
                    var tmpChannelName = TwitchChannelName.TrimEnd('/').Split('/').Last();
                    Plugin.Log($"Changing twitch channel to {tmpChannelName}");
                    TwitchChannelName = tmpChannelName;
                    Save();
                }
                TwitchChannelName = TwitchChannelName.ToLower().Replace(" ", "");
            }
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