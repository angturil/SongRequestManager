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
        public float LineSpacing = 2;
        public int MaxMessages = 20;

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
            {
                Load();
            }
            else
            {
                // If their old config exists, rename it then load their settings
                if (File.Exists("UserData\\BetterTwitchChat.ini"))
                {
                    File.Move("UserData\\BetterTwitchChat.ini", "UserData\\EnhancedTwitchChat.ini");
                    Load();
                    Plugin.Log("Migrated settings from BetterTwitchChat.ini to EnhancedTwitchChat.ini");
                }
                else
                {
                    string configSectionName = "BetterTwitchChat";
                    if (ModPrefs.GetString(configSectionName, "ChannelToJoin") != String.Empty && !ModPrefs.GetBool(configSectionName, "Migrated", false))
                    {
                        //TwitchoAuthToken = ModPrefs.GetString(configSectionName, "oAuth_Token", string.Empty);
                        //TwitchUsername = ModPrefs.GetString(configSectionName, "Username", string.Empty);
                        TwitchChannel = ModPrefs.GetString(configSectionName, "ChannelToJoin", String.Empty).ToLower().Replace(" ", "");
                        ChatPosition = new Vector3(ModPrefs.GetFloat(configSectionName, "PositionX", 2.0244143f), ModPrefs.GetFloat(configSectionName, "PositionY", 0.373768f), ModPrefs.GetFloat(configSectionName, "PositionZ", 0.08235432f));
                        ChatRotation = new Vector3(ModPrefs.GetFloat(configSectionName, "RotationX", 2.026023f), ModPrefs.GetFloat(configSectionName, "RotationY", 97.58616f), ModPrefs.GetFloat(configSectionName, "RotationZ", 1.190764f));
                        TextColor = new Color(ModPrefs.GetFloat(configSectionName, "TextColorRed", 1), ModPrefs.GetFloat(configSectionName, "TextColorGreen", 1), ModPrefs.GetFloat(configSectionName, "TextColorBlue", 1), ModPrefs.GetFloat(configSectionName, "TextColorAlpha", 1));
                        BackgroundColor = new Color(ModPrefs.GetFloat(configSectionName, "BackgroundRed", 0), ModPrefs.GetFloat(configSectionName, "BackgroundGreen", 0), ModPrefs.GetFloat(configSectionName, "BackgroundBlue", 0), ModPrefs.GetFloat(configSectionName, "BackgroundAlpha", 0.5f));
                        MaxMessages = ModPrefs.GetInt(configSectionName, "MaxChatLines", 20);
                        ChatWidth = ModPrefs.GetFloat(configSectionName, "ChatWidth", 160);
                        BackgroundPadding = ModPrefs.GetFloat(configSectionName, "BackgroundPadding", 4);
                        FontName = ModPrefs.GetString(configSectionName, "SystemFontName", "Segoe UI");
                        ReverseChatOrder = ModPrefs.GetBool(configSectionName, "ReverseChatOrder", false);
                        LockChatPosition = ModPrefs.GetBool(configSectionName, "LockChatPosition", false);

                        ModPrefs.SetBool(configSectionName, "Migrated", true);

                        Plugin.Log("Migrated old config settings to EnhancedTwitchChat.ini!");
                    }
                    Save();
                }
            }

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

        public void Save()
        {
            _saving = true;
            ConfigSerializer.SaveConfig(this, FilePath);
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

            if (MaxMessages < 1)
            {
                MaxMessages = 1;
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