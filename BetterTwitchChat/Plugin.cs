using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRUIControls;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Net;
using IllusionPlugin;
using UnityEngine.UI;
using TMPro;
using System.Collections.Concurrent;
using UnityEditor;
using BetterTwitchChat.Sprites;
using BetterTwitchChat.Utils;
using BetterTwitchChat.Chat;
using BetterTwitchChat.UI;

namespace BetterTwitchChat {
    public class Plugin : IPlugin {
        public string Name => "BetterTwitchChat";
        public string Version => "0.2.1";

        public bool IsAtMainMenu = false;
        public bool ShouldWriteConfig = false;
        public static Plugin Instance { get; private set; }
        public static int PixelsPerUnit = 100;
        public static string TwitchChannelID = string.Empty;
        public readonly Config Config = new Config(Path.Combine(Environment.CurrentDirectory, "UserData\\BetterTwitchChat.ini"));

        private ChatHandler _betterTwitchChat = null;

        // https://api.twitch.tv/kraken/streams/ninja?client_id=jg6ij5z8mf8jr8si22i5uq8tobnmde

        public static void Log(string msg) {
            msg = $"[BetterTwitchChat] {msg}";
            Console.WriteLine(msg);
        #if DEBUG
            using (StreamWriter w = File.AppendText($"BetterTwitchChat.log")) {
                w.WriteLine("{0}", msg);
            }
        #endif
        }

        public void OnApplicationStart() {
            Instance = this;
            
            // Pre-initialize our system fonts to reduce lag later on
            Drawing.InitSystemFonts();
        }

        public void OnApplicationQuit() {
            Config.Save();
        }

        public void OnLevelWasLoaded(int level) {
            string menuName = SceneManager.GetSceneByBuildIndex(level).name;
            if (menuName == "Menu") {
                if (!_betterTwitchChat) {
                    _betterTwitchChat = new GameObject("BetterTwitchChat").AddComponent<ChatHandler>();
                }

                if (!TwitchIRCClient.Initialized) {
                    new Thread(() => TwitchIRCClient.Initialize(_betterTwitchChat)).Start();
                    TwitchIRCClient.Initialized = true;
                }

                //Plugin.Log("Taking out the trash... ;)");
                System.GC.Collect();

                IsAtMainMenu = true;
            }
            else if (menuName.Contains("Environment")) {
                IsAtMainMenu = false;
            }
        }

        public void OnLevelWasInitialized(int level) {
            
        }
        
        public void OnUpdate() {
            if (ShouldWriteConfig) {
                Config.Save();
                ShouldWriteConfig = false;
            }
        }

        struct testStruct {
        };

        public void OnFixedUpdate() {
        }
    }
}
