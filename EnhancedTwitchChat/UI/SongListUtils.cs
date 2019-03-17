using BeatSaverDownloader.UI;
using CustomUI.BeatSaber;
using CustomUI.Utilities;
using EnhancedTwitchChat.Utils;
using HMUI;
//using SongBrowserPlugin;
//using SongBrowserPlugin.DataAccess;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace EnhancedTwitchChat
{
    class SongListUtils
    {
        private static LevelPackLevelsViewController _standardLevelListViewController = null;
        private static LevelPackDetailViewController _standardLevelDetailViewController = null;
        private static bool _initialized = false;
        private static bool _songBrowserInstalled = false;
        private static bool _songDownloaderInstalled = false;

        //private static List<IBeatmapLevel> CurrentLevels
        //{
        //    get
        //    {
        //        return ReflectionUtil.GetPrivateField<IBeatmapLevel[]>(_standardLevelListViewController, "_levels").ToList();
        //    }
        //    set
        //    {
        //        _standardLevelListViewController.SetLevels(value.ToArray());
        //    }
        //}

        public static void Initialize()
        {
            _standardLevelListViewController = Resources.FindObjectsOfTypeAll<LevelPackLevelsViewController>().FirstOrDefault();
            _standardLevelDetailViewController = Resources.FindObjectsOfTypeAll<LevelPackDetailViewController>().FirstOrDefault();

            if (!_initialized)
            {
                try
                {
                    _songBrowserInstalled = Utilities.IsModInstalled("Song Browser");
                    _songDownloaderInstalled = Utilities.IsModInstalled("BeatSaver Downloader");
                    _initialized = true;
                }
                catch (Exception e)
                {
                    Plugin.Log($"Exception {e}");
                }
            }
        }

        //private enum SongBrowserAction { Refresh = 1, ResetFilter = 2 }
        //private static void ExecuteSongBrowserAction(SongBrowserAction action)
        //{
        //    var _songBrowserUI = SongBrowserApplication.Instance.GetPrivateField<SongBrowserPlugin.UI.SongBrowserUI>("_songBrowserUI");
        //    if (_songBrowserUI)
        //    {
        //        if (action.HasFlag(SongBrowserAction.ResetFilter))
        //        {
        //            _songBrowserUI.Model.Settings.filterMode = SongFilterMode.None;
        //            if (!action.HasFlag(SongBrowserAction.Refresh))
        //                action |= SongBrowserAction.Refresh;
        //        }
        //        if (action.HasFlag(SongBrowserAction.Refresh))
        //        {
        //            _songBrowserUI.UpdateSongList();
        //            _songBrowserUI.RefreshSongList();
        //        }
        //    }
        //}

        private enum SongDownloaderAction { ResetFilter = 1 }
        private static void ExecuteSongDownloaderAction(SongDownloaderAction action)
        {
            if (action.HasFlag(SongDownloaderAction.ResetFilter))
            {
                SongListTweaks.Instance.SetLevels(SortMode.Newest, "");
            }
        }

        //public static void RemoveDuplicates()
        //{
        //   _standardLevelListViewController.SetLevels(CurrentLevels.Distinct().ToArray());
        //}

        public static IEnumerator RetrieveNewSong(string songFolderName, bool resetFilterMode = false)
        {
            if (!SongLoader.AreSongsLoaded) yield break;

            if (!_standardLevelListViewController) yield break;
            
            SongLoader.Instance.RetrieveNewSong(songFolderName);
            
            // If beatsaver downloader is installed and songbrowser isnt, then we need to change the filter mode through it
            if (resetFilterMode && _songDownloaderInstalled)
                ExecuteSongDownloaderAction(SongDownloaderAction.ResetFilter);

            //// Set the row index to the previously selected song
            //if (selectOldLevel)
            //    ScrollToLevel(selectedLevelId);
        }

        public static IEnumerator RefreshSongs(bool fullRefresh = false, bool selectOldLevel = true, bool resetFilterMode = false)
        {
            if (!SongLoader.AreSongsLoaded) yield break;

            if (!_standardLevelListViewController) yield break;

            // // Grab the currently selected level id so we can restore it after refreshing
            // string selectedLevelId = _standardLevelListViewController.selectedLevel?.levelID;

            // Wait until song loader is finished loading, then refresh the song list
            while (SongLoader.AreSongsLoading) yield return null;
            SongLoader.Instance.RefreshSongs(fullRefresh);
            while (SongLoader.AreSongsLoading) yield return null;

            // If song browser is installed, update/refresh it
            //if (_songBrowserInstalled)
            //    ExecuteSongBrowserAction(resetFilterMode ? SongBrowserAction.ResetFilter : SongBrowserAction.Refresh);
            //else 

            // If beatsaver downloader is installed and songbrowser isnt, then we need to change the filter mode through it
            if (resetFilterMode && _songDownloaderInstalled)
                ExecuteSongDownloaderAction(SongDownloaderAction.ResetFilter);

            //// Set the row index to the previously selected song
            //if (selectOldLevel)
            //    ScrollToLevel(selectedLevelId);
        }

        public static int GetLevelIndex(LevelPackLevelsViewController table, string levelID)
        {
            for (int i = 0; i < table.levelPack.beatmapLevelCollection.beatmapLevels.Length; i++)
            {
                if (table.levelPack.beatmapLevelCollection.beatmapLevels[i].levelID == levelID)
                {
                    return i + 1;
                }
            }
            return -1;
        }

        public static IBeatmapLevelPack GetLevelPackWithLevels(BeatmapLevelSO[] levels, string packName = null, Sprite packCover = null)
        {
            CustomLevelCollectionSO levelCollection = ScriptableObject.CreateInstance<CustomLevelCollectionSO>();
            levelCollection.SetPrivateField("_levelList", levels.ToList());
            levelCollection.SetPrivateField("_beatmapLevels", levels);
            
            CustomBeatmapLevelPackSO pack = CustomBeatmapLevelPackSO.GetPack(levelCollection);
            pack.SetPrivateField("_packName", string.IsNullOrEmpty(packName) ? "Custom Songs" : packName);
            pack.SetPrivateField("_coverImage", UIUtilities.BlankSprite);
            pack.SetPrivateField("_isPackAlwaysOwned", true);
            return pack;
        }

        public static IEnumerator ScrollToLevel(string levelID, Action<bool> callback, bool isRetry = false)
        {
            if (_standardLevelListViewController)
            {
                TableView tableView = _standardLevelListViewController.GetComponentInChildren<TableView>();
                tableView.ReloadData();

                var levels = _standardLevelListViewController.levelPack.beatmapLevelCollection.beatmapLevels.Where(l => l.levelID == levelID).ToArray();

                if (levels.Length > 0)
                {
                    int row = GetLevelIndex(_standardLevelListViewController, levelID);
                    if (row != -1)
                    {
                        tableView.SelectCellWithIdx(row, true);
                        tableView.ScrollToCellWithIdx(row, TableView.ScrollPositionType.Beginning, true);
                        callback?.Invoke(true);
                        yield break;
                    }
                }
            }

            if (!isRetry)
            {
                yield return SongListUtils.RefreshSongs(false, false, true);
                yield return ScrollToLevel(levelID, callback, true);
                yield break;
            }

            var tempLevels = SongLoader.CustomLevels.Where(l => l.levelID == levelID).ToArray();
            foreach (CustomLevel l in tempLevels)
                SongLoader.CustomLevels.Remove(l);

            Plugin.Log($"Failed to scroll to {levelID}!");
            callback?.Invoke(false);
        }
    }
}
