using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using IllusionPlugin;
using EnhancedTwitchChat.Chat;
using EnhancedTwitchChat.UI;
using System.Threading.Tasks;

namespace EnhancedTwitchChat
{
    public class Plugin : IPlugin
    {
        public string Name => "EnhancedTwitchChat";
        public string Version => "1.1.0";

        public bool IsAtMainMenu = true;
        public static Plugin Instance { get; private set; }
        private readonly Config Config = new Config(Path.Combine(Environment.CurrentDirectory, "UserData\\EnhancedTwitchChat.ini"));
        
        public static void Log(string msg)
        {
            Console.WriteLine($"[EnhancedTwitchChat] {msg}");
        }

        public void OnApplicationStart()
        {
            if (Instance != null) return;
            Instance = this;

            ChatHandler.OnLoad();
            Task.Run(() => TwitchIRCClient.Initialize());

            SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        }

        private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            if (arg0.name == "Menu")
            {
                Settings.OnLoad();
                RequestBot.OnLoad();
            }
        }

        public void OnApplicationQuit()
        {
        }

        private void SceneManager_activeSceneChanged(Scene from, Scene to)
        {
            if (from.name == "EmptyTransition" && to.name == "Menu")
                Config.Save(true);
            if (to.name == "Menu")
                IsAtMainMenu = true;
            else if (to.name == "GameCore")
                IsAtMainMenu = false;

            try
            {
                ChatHandler.Instance?.SceneManager_activeSceneChanged(from, to);
            }
            catch { }
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
        }
    }
}
