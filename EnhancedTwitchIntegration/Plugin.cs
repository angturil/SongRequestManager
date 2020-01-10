using IPA;
using IPALogger = IPA.Logging.Logger;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine.SceneManagement;
using SongRequestManager.UI;
using BeatSaberMarkupLanguage.Settings;
using SongBrowser;
using IPA.Utilities;

namespace SongRequestManager
{
    public class Plugin : IBeatSaberPlugin
    {
        public string Name => "Song Request Manager";
        public static SemVer.Version Version => IPA.Loader.PluginManager.GetPluginFromId("SongRequestManager").Metadata.Version;

        public static IPALogger Logger { get; internal set; }

        internal static WebClient WebClient;

        public bool IsAtMainMenu = true;
        public bool IsApplicationExiting = false;
        public static Plugin Instance { get; private set; }
        
        private readonly RequestBotConfig RequestBotConfig = new RequestBotConfig();

        public static string DataPath = Path.Combine(Environment.CurrentDirectory, "UserData", "StreamCore");
        public static bool SongBrowserPluginPresent;

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

            Dispatcher.Initialize();

            // create our internal webclient
            WebClient = new WebClient();

            SongBrowserPluginPresent = IPA.Loader.PluginManager.GetPlugin("Song Browser") != null;

            // setup handle for fresh menu scene changes
            BS_Utils.Utilities.BSEvents.OnLoad();
            BS_Utils.Utilities.BSEvents.menuSceneLoadedFresh += OnMenuSceneLoadedFresh;

            // keep track of active scene
            BS_Utils.Utilities.BSEvents.menuSceneActive += () => { IsAtMainMenu = true; };
            BS_Utils.Utilities.BSEvents.gameSceneActive += () => { IsAtMainMenu = false; };

            // init sprites
            Base64Sprites.Init();
        }

        private void OnMenuSceneLoadedFresh()
        {
            // setup settings ui
            BSMLSettings.instance.AddSettingsMenu("SRM", "SongRequestManager.Views.SongRequestManagerSettings.bsml", SongRequestManagerSettings.instance);

            // main load point
            RequestBot.OnLoad();
            RequestBotConfig.Save(true);
        }

        public static void SongBrowserCancelFilter()
        {
            if (SongBrowserPluginPresent)
            {
                var _songBrowserUI = SongBrowser.SongBrowserApplication.Instance.GetPrivateField<SongBrowser.UI.SongBrowserUI>("_songBrowserUI");
                if (_songBrowserUI)
                {
                    if (_songBrowserUI.Model.Settings.filterMode != SongBrowser.DataAccess.SongFilterMode.None && _songBrowserUI.Model.Settings.sortMode != SongBrowser.DataAccess.SongSortMode.Original)
                    {
                        _songBrowserUI.CancelFilter();
                    }
                }
                else
                {
                    Plugin.Log("There was a problem obtaining SongBrowserUI object, unable to reset filters");
                }
            }
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
