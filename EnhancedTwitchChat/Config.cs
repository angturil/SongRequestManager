using IllusionPlugin;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedTwitchChat
{
    public class Config
    {
        public string FilePath { get; }

        public string TwitchChannel = "";
        //public string TwitchUsername = "";
        //public string TwitchoAuthToken = "";
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
        public bool SongRequestBot = false;
        public bool AnimatedEmotes = true;

        public string RequestCommandAliases = "request,bsr,add";
        public int RequestLimit = 5;
        public int RequestCooldownMinutes = 5;

        public event Action<Config> ConfigChangedEvent;

        private readonly FileSystemWatcher _configWatcher;
        private bool _saving;

        public static Config Instance = null;

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

            if (File.Exists(filePath))
                Load();
            else
                Save();

            _configWatcher = new FileSystemWatcher($"{Environment.CurrentDirectory}\\UserData")
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
            if (TwitchChannel.Length > 0)
                TwitchChannel = TwitchChannel.ToLower().Replace(" ", "");
            //else {
            //TwitchChannel = TwitchUsername;
            //}
            if (BackgroundPadding < 0)
            {
                BackgroundPadding = 0;
            }

            if (MaxChatLines < 1)
            {
                MaxChatLines = 1;
            }
        }

        public void Save(bool callback = false)
        {
            if(!callback)
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