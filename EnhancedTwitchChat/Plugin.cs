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
        public string Version => "0.3.5";

        public bool IsAtMainMenu = true;
        public bool ShouldWriteConfig = false;
        public static Plugin Instance { get; private set; }

        public readonly Config Config = new Config(Path.Combine(Environment.CurrentDirectory, "UserData\\EnhancedTwitchChat.ini"));


        // https://api.twitch.tv/kraken/streams/ninja?client_id=jg6ij5z8mf8jr8si22i5uq8tobnmde

        public static void Log(string msg) {
            Console.WriteLine($"[EnhancedTwitchChat] {msg}");
        }

        public void OnApplicationStart() {
            if (Instance != null) return;

            Instance = this;

            new GameObject("EnhancedTwitchChat").AddComponent<ChatHandler>();
            new Thread(() => TwitchIRCClient.Initialize()).Start();

            SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
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

        public void OnApplicationQuit() {
            Config.Save();
        }
        
        public void OnLevelWasInitialized(int level) {
        }
        
        public void OnFixedUpdate() {
        }

        public void OnUpdate()
        {
            if (ShouldWriteConfig)
            {
                Config.Save();
                ShouldWriteConfig = false;
            }
            
            var c = GameObject.Find("Camera Plus")?.gameObject.GetComponent<Camera>();
            if (c && !c.GetComponent<ManualCameraRenderer>())
                c.gameObject.AddComponent<ManualCameraRenderer>();
        }
    }

    public class ManualCameraRenderer : MonoBehaviour
    {
        public float fps = 60;
        float elapsed;
        Camera cam;

        void Start()
        {
            cam = GetComponent<Camera>();
            cam.enabled = false;
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed > 1.0f / fps)
            {
                elapsed = 0;
                cam.enabled = true;
            }
            else if (cam.enabled)
            {
                cam.enabled = false;
            }
        }
    }
}
