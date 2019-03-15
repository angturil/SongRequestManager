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

namespace EnhancedTwitchChat
{
    public class Plugin : IPlugin
    {
        public string Name => "EnhancedTwitchChat";
        public string Version => "1.1.5";

        public bool IsAtMainMenu = true;
        public bool IsApplicationExiting = false;
        public static Plugin Instance { get; private set; }
        private readonly Config Config = new Config(Path.Combine(Environment.CurrentDirectory, "UserData\\EnhancedTwitchChat.ini"));

        public static void Log(string text,
                        [CallerFilePath] string file = "",
                        [CallerMemberName] string member = "",
                        [CallerLineNumber] int line = 0)
        {
            Debug.Log($"[EnhancedTwitchChat] {DateTime.UtcNow} {Path.GetFileName(file)}->{member}({line}): {text}"); // Added time stamp. Might be too verbose
        }
        
        public void OnApplicationStart()
        {
            if (Instance != null) return;
            Instance = this;

            SharedCoroutineStarter.instance.StartCoroutine(DelayedStartup());
        }

        #if OLDVERSION
        static string menucore = "Menu";
        #else
        static string menucore = "MenuCore";
        #endif

        private IEnumerator DelayedStartup()
        {
            yield return new WaitForSeconds(0.5f);

            ChatHandler.OnLoad();
            Task.Run(() => TwitchWebSocketClient.Initialize());

            SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;

            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == menucore);
            if(Config.Instance.TwitchChannelName == String.Empty)
                yield return new WaitUntil(() => BeatSaberUI.DisplayKeyboard("Enter Your Twitch Channel Name!", String.Empty, null, (channelName) => { Config.Instance.TwitchChannelName = channelName; Config.Instance.Save(true); }));
        }
        
        private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            if (arg0.name == menucore)
            {
                Settings.OnLoad();
#if REQUEST_BOT
                RequestBot.OnLoad();
#endif
            }
        }

        public void OnApplicationQuit()
        {
            IsApplicationExiting = true;
            TwitchWebSocketClient.Shutdown();
        }

        private void SceneManager_activeSceneChanged(Scene from, Scene to)
        {

            Resources.FindObjectsOfTypeAll<TMP_FontAsset>().ToList().ForEach(a => Log($"Font: {a.name}"));
            if (from.name == "EmptyTransition"  && to.name == menucore)
                Config.Save(true);
            if (to.name == menucore)
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
