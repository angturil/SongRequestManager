
using SongRequestManager;
using SongRequestManager;
using IllusionPlugin;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine.SceneManagement;
using StreamCore.Chat;

namespace SongRequestManager
{
    public class Plugin : IPlugin
    {
        public string Name => "SongRequestManager";
        public string Version => "1.3.3";


        public bool IsAtMainMenu = true;
        public bool IsApplicationExiting = false;
        public static Plugin Instance { get; private set; }
        
        private readonly RequestBotConfig RequestBotConfig = new RequestBotConfig();

        public static void Log(string text,
                        [CallerFilePath] string file = "",
                        [CallerMemberName] string member = "",
                        [CallerLineNumber] int line = 0)
        {
            Console.WriteLine($"[SongRequestManager] {DateTime.UtcNow} {Path.GetFileName(file)}->{member}({line}): {text}");
        }

        public void OnApplicationStart()
        {
            if (Instance != null) return;
            Instance = this;

            TwitchWebSocketClient.Initialize();

            SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        }

        static string MenuSceneName = "MenuCore";
        
        private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            if (arg0.name == MenuSceneName)
            {
                try
                {
                    Settings.OnLoad();
                }
                catch (Exception ex)       
                {
                    Plugin.Log($"{ex}");
                }

                RequestBot.OnLoad();

                RequestBotConfig.Save(true);
            }
        }

        public void OnApplicationQuit()
        {
            IsApplicationExiting = true;
        }

        private void SceneManager_activeSceneChanged(Scene from, Scene to)
        {
            if (to.name == MenuSceneName)
                IsAtMainMenu = true;
            else if (to.name == "GameCore")
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
        }
    }
}
