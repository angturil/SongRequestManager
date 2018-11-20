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
using EnhancedTwitchChat.Sprites;
using EnhancedTwitchChat.Utils;
using EnhancedTwitchChat.Chat;
using EnhancedTwitchChat.UI;
using AsyncTwitch;

namespace EnhancedTwitchChat {
    public class Plugin : IPlugin {
        public string Name => "EnhancedTwitchChat";
        public string Version => "0.3.3";

        public bool IsAtMainMenu = false;
        public bool ShouldWriteConfig = false;
        public static Plugin Instance { get; private set; }

        public readonly Config Config = new Config(Path.Combine(Environment.CurrentDirectory, "UserData\\EnhancedTwitchChat.ini"));


        // https://api.twitch.tv/kraken/streams/ninja?client_id=jg6ij5z8mf8jr8si22i5uq8tobnmde

        public static void Log(string msg) {
            Console.WriteLine($"[EnhancedTwitchChat] {msg}");
        }

        public void OnApplicationStart() {
            Instance = this;

            new GameObject("EnhancedTwitchChat").AddComponent<ChatHandler>();
            new Thread(() => TwitchIRCClient.Initialize()).Start();
        }

        public void OnApplicationQuit() {
            Config.Save();
        }

        public void OnLevelWasLoaded(int level) {
            string menuName = SceneManager.GetSceneByBuildIndex(level).name;
            if (menuName == "Menu") {
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
        
        public void OnFixedUpdate() {
        }
    }
}
