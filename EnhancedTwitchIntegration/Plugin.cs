
using EnhancedTwitchIntegration.Bot;
using EnhancedTwitchIntegration.Config;
using IllusionPlugin;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine.SceneManagement;

namespace EnhancedTwitchIntegration
{
    public class Plugin : IPlugin
    {
        public string Name => "EnhancedTwitchIntegration";
        public string Version => "0.0.1";


        public bool IsAtMainMenu = true;
        public bool IsApplicationExiting = false;
        public static Plugin Instance { get; private set; }
        
        private readonly RequestBotConfig RequestBotConfig = new RequestBotConfig();

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

            SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        }

#if OLDVERSION
        static string MenuSceneName = "Menu";
#else
        static string MenuSceneName = "MenuCore";
#endif
        
        private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            if (arg0.name == MenuSceneName)
            {
                //Settings.OnLoad();
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
