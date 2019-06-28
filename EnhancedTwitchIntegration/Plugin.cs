using IPA;
using IPALogger = IPA.Logging.Logger;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine.SceneManagement;
using StreamCore.Chat;

namespace SongRequestManager
{
    public class Plugin : IBeatSaberPlugin
    {
        public string Name => "Song Request Manager";
        public string Version => "2.0.8";

        public static IPALogger Logger { get; internal set; }

        public bool IsAtMainMenu = true;
        public bool IsApplicationExiting = false;
        public static Plugin Instance { get; private set; }
        
        private readonly RequestBotConfig RequestBotConfig = new RequestBotConfig();

        public static string DataPath = Path.Combine(Environment.CurrentDirectory, "UserData", "StreamCore");

        public void Init(object thisIsNull, IPALogger log)
        {
            Logger = log;
        }

        public static void Log(string text,
                        [CallerFilePath] string file = "",
                        [CallerMemberName] string member = "",
                        [CallerLineNumber] int line = 0)
        {
            Logger.Info($"[SongRequestManager] {Path.GetFileName(file)}->{member}({line}): {text}");
        }

        public void OnApplicationStart()
        {
            if (Instance != null) return;
            Instance = this;

            TwitchWebSocketClient.Initialize();
        }

        static string MenuSceneName = "MenuCore";
        
        public void OnSceneLoaded(Scene arg0, LoadSceneMode arg1)
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

        public void OnActiveSceneChanged(Scene from, Scene to)
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

        public void OnSceneUnloaded(Scene scene)
        {
        }
    }
}
