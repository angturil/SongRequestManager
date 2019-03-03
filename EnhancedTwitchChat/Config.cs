using IllusionPlugin;
using SimpleJSON;
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

        public string RequestCommandAliases = "request,bsr,add";

        public int RequestLimit = 5;
        public int SubRequestLimit = 5;
        public int ModRequestLimit = 10;
        public int VipRequestLimit = 3; // currently ignored, vip's get +1 over base will discuss
        public int RequestCooldownMinutes = 0;

        public string SongRequestQueue = "";
        public string SongBlacklist = "";
        public string DeckList = "fun hard challenge dance";

        public float lowestallowedrating = 0.1f; // Lowest allowed song rating to be played 

        public bool AutopickFirstSong = false; // Pick the first song that !bsr finds instead of showing a short list.
        public bool AllowModAddClosedQueue = true; // Allow moderator to add songs while queue is closed
        public bool SendNextSongBeingPlayedtoChat = true; // Enable chat message when you hit play
        public bool ApplyAllFiltersToBroadcaster = false;
        public bool UpdateQueueStatusFiles = true; // Create and update queue list and open/close status files for OBS

        public int maxaddnewscanrange = 80; // How far down the list to scan
        public int maxaddnewresults = 10;  // Max results per command


        public event Action<Config> ConfigChangedEvent;

        private readonly FileSystemWatcher _configWatcher;
        private bool _saving;

        public static Config Instance = null;

        public HashSet<string> Blacklist
        {
            get
            {
                HashSet<string> blacklist = new HashSet<string>();
                if (SongBlacklist != String.Empty)
                {
                    foreach (string s in SongBlacklist.Split(','))
                        blacklist.Add(s);
                }
                return blacklist;
            }
            set
            {
                SongBlacklist = string.Join(",", value.Distinct());
                Save();
            }
        }
        

        public List<string> RequestQueue
        {
            get
            {
                List<string> queue = new List<string>();
                if (SongRequestQueue != String.Empty)
                {
                    foreach (string s in SongRequestQueue.Split(','))
                        queue.Add(s);
                }
                return queue;
            }
            set
            {
                SongRequestQueue = string.Join(",", value.Distinct());
                Save();
            }
        }

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

            if(!Directory.Exists(Path.GetDirectoryName(FilePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

            if (File.Exists(FilePath))
            {
                Load();
                
                if (File.ReadAllText(FilePath).Contains("TwitchChannel="))
                {
                    var oldConfig = new OldConfigOptions();
                    ConfigSerializer.LoadConfig(oldConfig, FilePath);

                    TwitchChannelName = oldConfig.TwitchChannel;
                }
            }
            CorrectConfigSettings();
            Save();

            _configWatcher = new FileSystemWatcher(Path.Combine(Environment.CurrentDirectory, "UserData"))
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