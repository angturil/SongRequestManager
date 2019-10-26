using IPA;
using IPALogger = IPA.Logging.Logger;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine.SceneManagement;
using StreamCore.Chat;
using SongRequestManager.UI;

namespace SongRequestManager
{
    public class Plugin : IBeatSaberPlugin
    {
        public string Name => "Song Request Manager";
        public string Version => "2.1.4";

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

             // setup handle for fresh menu scene changes
            CustomUI.Utilities.BSEvents.OnLoad();
            CustomUI.Utilities.BSEvents.menuSceneLoadedFresh += OnMenuSceneLoadedFresh;
            
                        // keep track of active scene
            CustomUI.Utilities.BSEvents.menuSceneActive += () => { IsAtMainMenu = true; };
            CustomUI.Utilities.BSEvents.gameSceneActive += () => { IsAtMainMenu = false; };

            // init sprites
            Base64Sprites.Init();
        }



        private void OnMenuSceneLoadedFresh()
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


        #region Unused IBeatSaberPlugin methods
        public void OnSceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
        }

        public void OnApplicationQuit()
        {
            IsApplicationExiting = true;
        }

        public void OnActiveSceneChanged(Scene from, Scene to)
        {
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
        #endregion
    }
}
