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
using EnhancedTwitchChat.Textures;
using EnhancedTwitchChat.Utils;
using EnhancedTwitchChat.Chat;
using EnhancedTwitchChat.UI;
using AsyncTwitch;

namespace EnhancedTwitchChat
{
    public class Plugin : IPlugin
    {
        public string Name => "EnhancedTwitchChat";
        public string Version => "1.0.0";

        public bool IsAtMainMenu = true;
        public bool ShouldWriteConfig = false;
        public static Plugin Instance { get; private set; }

        private readonly Config Config = new Config(Path.Combine(Environment.CurrentDirectory, "UserData\\EnhancedTwitchChat.ini"));


        // https://api.twitch.tv/kraken/streams/ninja?client_id=jg6ij5z8mf8jr8si22i5uq8tobnmde

        public static void Log(string msg)
        {
            Console.WriteLine($"[EnhancedTwitchChat] {msg}");
        }

        public void OnApplicationStart()
        {
            if (Instance != null) return;

            Instance = this;

            new GameObject("EnhancedTwitchChatChatHandler").AddComponent<ChatHandler>();
            new Thread(() => TwitchIRCClient.Initialize()).Start();

            SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
        }

        public void OnApplicationQuit()
        {
        }

        private void SceneManager_activeSceneChanged(Scene arg0, Scene arg1)
        {
            if (arg1.name == "Menu")
                IsAtMainMenu = true;

            else if (arg1.name == "GameCore")
                IsAtMainMenu = false;
        }

        public void OnLevelWasLoaded(int level)
        {
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnFixedUpdate()
        {
        }

        public void OnUpdate()
        {
            if (ShouldWriteConfig)
            {
                Config.Save();
                ShouldWriteConfig = false;
            }
        }
    }
}
