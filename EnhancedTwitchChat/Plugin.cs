using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using IllusionPlugin;
using EnhancedTwitchChat.Chat;
using EnhancedTwitchChat.UI;
using System.Threading.Tasks;
using System.Collections;
using CustomUI.BeatSaber;
using EnhancedTwitchChat.Bot;
using System.Runtime.CompilerServices;
using TMPro;
using EnhancedTwitchChat.Config;

namespace EnhancedTwitchChat
{
    public class Plugin : IPlugin
    {
        public string Name => "EnhancedTwitchChat";
        public string Version => "1.2.0-beta3";

        public bool IsAtMainMenu = true;
        public bool IsApplicationExiting = false;
        public static Plugin Instance { get; private set; }

        private readonly ChatConfig ChatConfig = new ChatConfig();
        private readonly RequestBotConfig RequestBotConfig = new RequestBotConfig();
        private readonly TwitchLoginConfig TwitchLoginConfig = new TwitchLoginConfig();

        public static void Log(string text,
                        [CallerFilePath] string file = "",
                        [CallerMemberName] string member = "",
                        [CallerLineNumber] int line = 0)
        {
            Console.WriteLine($"[EnhancedTwitchChat] {DateTime.UtcNow} {Path.GetFileName(file)}->{member}({line}): {text}");
        }
        
        public void OnApplicationStart()
        {
            if (Instance != null) return;
            Instance = this;

            SharedCoroutineStarter.instance.StartCoroutine(DelayedStartup());
        }

        #if OLDVERSION
        static string MenuSceneName = "Menu";
        #else
        static string MenuSceneName = "MenuCore";
        #endif

        private IEnumerator DelayedStartup()
        {
            yield return new WaitForSeconds(0.5f);

            ChatHandler.OnLoad();
            Task.Run(() => TwitchWebSocketClient.Initialize());

            SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;

            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == MenuSceneName);
            if (TwitchLoginConfig.Instance.TwitchChannelName == String.Empty)
                yield return new WaitUntil(() => BeatSaberUI.DisplayKeyboard("Enter Your Twitch Channel Name!", String.Empty, null, (channelName) => { TwitchLoginConfig.Instance.TwitchChannelName = channelName; TwitchLoginConfig.Instance.Save(true); }));
        }
        
        private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            if (arg0.name == MenuSceneName)
            {
                Settings.OnLoad();
#if REQUEST_BOT
                RequestBot.OnLoad();
#endif

                ChatConfig.Save(true);
                RequestBotConfig.Save(true);
                TwitchLoginConfig.Save(true);
            }
        }

        public void OnApplicationQuit()
        {
            IsApplicationExiting = true;
            TwitchWebSocketClient.Shutdown();
        }

        private void SceneManager_activeSceneChanged(Scene from, Scene to)
        {
            if (to.name == MenuSceneName)
                IsAtMainMenu = true;
            else if (to.name == "GameCore")
                IsAtMainMenu = false;

            try
            {
                ChatHandler.Instance?.SceneManager_activeSceneChanged(from, to);
            }
            catch {}
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
