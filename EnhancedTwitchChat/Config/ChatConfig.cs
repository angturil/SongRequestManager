//using EnhancedTwitchChat.Bot;
using IllusionPlugin;
using EnhancedTwitchChat.SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
#if REQUEST_BOT
using EnhancedTwitchIntegration.Config;
using EnhancedTwitchIntegration.Bot;
#endif

namespace EnhancedTwitchChat.Config
{
    public class OldConfigOptions
    {
        public string TwitchChannel = "";
    }

    public class OldBlacklistOption
    {
        public string SongBlacklist;
    }

    public class SemiOldConfigOptions
    {
        public string TwitchChannelName = "";
        public string TwitchUsername = "";
        public string TwitchOAuthToken = "";
        public bool SongRequestBot = false;
        public bool PersistentRequestQueue = true;
    }
    
    public class ChatConfig
    {
        private string FilePath = Path.Combine(Environment.CurrentDirectory, "UserData", "EnhancedTwitchChat", "EnhancedTwitchChat.ini");


        public string FontName = "Segoe UI";
        //public int BombBitValue;
        //public int TwitchBitBalance;

        public float ChatScale = 1.1f;
        public float ChatWidth = 160;
        public float LineSpacing = 2.0f;
        public int MaxChatLines = 30;
        
        public float PositionX = 0;
        public float PositionY = 2.6f;
        public float PositionZ = 2.3f;

        public float RotationX = -30;
        public float RotationY = 0;
        public float RotationZ = 0;

        public float TextColorR = 1;
        public float TextColorG = 1;
        public float TextColorB = 1;
        public float TextColorA = 1;

        public float BackgroundColorR = 0;
        public float BackgroundColorG = 0;
        public float BackgroundColorB = 0;
        public float BackgroundColorA = 0.6f;
        public float BackgroundPadding = 4;

        public bool AnimatedEmotes = true;
        public bool DrawShadows = false;
        public bool LockChatPosition = false;
        public bool ReverseChatOrder = false;
        public bool FilterCommandMessages = false;
        public bool FilterBroadcasterMessages = false;
        public bool FilterUserlistMessages = true; // Filter messages in chatexclude.users ( pick a better name ) 

      
        public event Action<ChatConfig> ConfigChangedEvent;

        private readonly FileSystemWatcher _configWatcher;
        private bool _saving;

        private static ChatConfig _instance = null;
        public static ChatConfig Instance {
            get
            {
                if (_instance == null)
                    _instance = new ChatConfig();
                return _instance;
            }

            private set
            {
                _instance = value;
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

        public ChatConfig()
        {
            Instance = this;

            if (!Directory.Exists(Path.GetDirectoryName(FilePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

            string oldFilePath = Path.Combine(Environment.CurrentDirectory, "UserData", "EnhancedTwitchChat.ini");
            if(File.Exists(oldFilePath) && !File.Exists(FilePath))
            {
                File.Move(oldFilePath, FilePath);
            }

            if (File.Exists(FilePath))
            {
                Load();

                var text = File.ReadAllText(FilePath);
                if (text.Contains("TwitchUsername="))
                {
                    SemiOldConfigOptions semiOldConfigInfo = new SemiOldConfigOptions();
                    ConfigSerializer.LoadConfig(semiOldConfigInfo, FilePath);

                    TwitchLoginConfig.Instance.TwitchChannelName = semiOldConfigInfo.TwitchChannelName;
                    TwitchLoginConfig.Instance.TwitchUsername = semiOldConfigInfo.TwitchUsername;
                    TwitchLoginConfig.Instance.TwitchOAuthToken = semiOldConfigInfo.TwitchOAuthToken;
                    TwitchLoginConfig.Instance.Save(true);

                    if (Plugin.Instance.RequestBotInstalled)
                    {
                        UpdateRequestBotConfig(ref semiOldConfigInfo);
                    }
                }

                if (Plugin.Instance.RequestBotInstalled)
                {
                    if (text.Contains("SongBlacklist="))
                    {
                        UpdateRequestBotBlacklist();
                    }
                }
            }
            CorrectConfigSettings();
            Save();

            _configWatcher = new FileSystemWatcher(Path.GetDirectoryName(FilePath))
            {
                NotifyFilter = NotifyFilters.LastWrite,
                Filter = "EnhancedTwitchChat.ini",
                EnableRaisingEvents = true
            };
            _configWatcher.Changed += ConfigWatcherOnChanged;
        }

        private void UpdateRequestBotBlacklist()
        {
#if REQUEST_BOT
            var oldConfig = new OldBlacklistOption();
            ConfigSerializer.LoadConfig(oldConfig, FilePath);

            if (oldConfig.SongBlacklist.Length > 0)
                SongBlacklist.ConvertFromList(oldConfig.SongBlacklist.Split(','));
#endif
        }

        private void UpdateRequestBotConfig(ref SemiOldConfigOptions semiOldConfigInfo)
        {
#if REQUEST_BOT
            RequestBotConfig.Instance.RequestBotEnabled = semiOldConfigInfo.SongRequestBot;
            RequestBotConfig.Instance.PersistentRequestQueue = semiOldConfigInfo.PersistentRequestQueue;
            RequestBotConfig.Instance.Save(true);
#endif
        }

        ~ChatConfig()
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
        
        private void CorrectConfigSettings()
        {
            if (BackgroundPadding < 0)
                BackgroundPadding = 0;
            if (MaxChatLines < 1)
                MaxChatLines = 1;
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