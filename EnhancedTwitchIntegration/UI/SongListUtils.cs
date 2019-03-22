using BeatSaverDownloader.UI;
using CustomUI.BeatSaber;
//using CustomUI.Utilities;
using EnhancedTwitchChat.Utils;
using HMUI;
using SongBrowserPlugin;
using SongBrowserPlugin.DataAccess;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace EnhancedTwitchIntegration
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

        private enum SongBrowserAction { ResetFilter = 1 }
        private static void ExecuteSongBrowserAction(SongBrowserAction action)
        {
            var _songBrowserUI = SongBrowserApplication.Instance.GetPrivateField<SongBrowserPlugin.UI.SongBrowserUI>("_songBrowserUI");
            if (_songBrowserUI)
            {
                if (action.HasFlag(SongBrowserAction.ResetFilter))
                {
                    _songBrowserUI.Model.Settings.filterMode = SongFilterMode.None;
                }
            }
        }

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
            if (resetFilterMode)
            {
                // If song browser is installed, update/refresh it
                if (_songBrowserInstalled)
                    ExecuteSongBrowserAction(SongBrowserAction.ResetFilter);
                // If beatsaver downloader is installed and songbrowser isnt, then we need to change the filter mode through it
                else if (_songDownloaderInstalled)
                    ExecuteSongDownloaderAction(SongDownloaderAction.ResetFilter);
            }

            //// Set the row index to the previously selected song
            //if (selectOldLevel)
            //    ScrollToLevel(selectedLevelId);
        }

        public static IEnumerator RefreshSongs(bool fullRefresh = false, bool selectOldLevel = true)
        {
            if (!SongLoader.AreSongsLoaded) yield break;
            if (!_standardLevelListViewController) yield break;

            // // Grab the currently selected level id so we can restore it after refreshing
            // string selectedLevelId = _standardLevelListViewController.selectedLevel?.levelID;

            // Wait until song loader is finished loading, then refresh the song list
            while (SongLoader.AreSongsLoading) yield return null;
            SongLoader.Instance.RefreshSongs(fullRefresh);
            while (SongLoader.AreSongsLoading) yield return null;
            

            //// Set the row index to the previously selected song
            //if (selectOldLevel)
            //    ScrollToLevel(selectedLevelId);
        }

        public static void SelectCustomSongPack(bool resetFilters = true)
        {
            var levelPacksTableView = Resources.FindObjectsOfTypeAll<LevelPacksTableView>().First();
            var tableView = levelPacksTableView.GetPrivateField<TableView>("_tableView");
            
            var packsCollection = levelPacksTableView.GetPrivateField<IBeatmapLevelPackCollection>("_levelPackCollection");
            int customSongPackIndex = -1;
            for(int i=0; i< packsCollection.beatmapLevelPacks.Length; i++)
                if(packsCollection.beatmapLevelPacks[i].packName == "Custom Maps")
                    customSongPackIndex = i;

            if (customSongPackIndex != -1 && levelPacksTableView.GetPrivateField<int>("_selectedColumn") != customSongPackIndex)
            {
                tableView.SelectCellWithIdx(customSongPackIndex, true);
                tableView.ScrollToCellWithIdx(0, TableView.ScrollPositionType.Beginning, false);
                for (int i = 0; i < Mathf.FloorToInt(customSongPackIndex / 4); i++)
                    tableView.PageScrollDown();
            }

            // If song browser is installed, update/refresh it
            if (_songBrowserInstalled)
                ExecuteSongBrowserAction(SongBrowserAction.ResetFilter);
            // If beatsaver downloader is installed and songbrowser isnt, then we need to change the filter mode through it
            else if (_songDownloaderInstalled)
                ExecuteSongDownloaderAction(SongDownloaderAction.ResetFilter);
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
        
        public static IEnumerator ScrollToLevel(string levelID, Action<bool> callback, bool animated, bool isRetry = false)
        {
            if (_standardLevelListViewController)
            {
                // Make sure our custom songpack is selected
                SelectCustomSongPack();

                TableView tableView = _standardLevelListViewController.GetComponentInChildren<TableView>();
                tableView.ReloadData();

                var levels = _standardLevelListViewController.levelPack.beatmapLevelCollection.beatmapLevels.Where(l => l.levelID == levelID).ToArray();

                if (levels.Length > 0)
                {
                    int row = GetLevelIndex(_standardLevelListViewController, levelID);
                    if (row != -1)
                    {
                        tableView.SelectCellWithIdx(row, true);
                        tableView.ScrollToCellWithIdx(row, TableView.ScrollPositionType.Beginning, animated);
                        callback?.Invoke(true);
                        yield break;
                    }
                }
            }

            if (!isRetry)
            {
                yield return SongListUtils.RefreshSongs(false, false);
                yield return ScrollToLevel(levelID, callback, animated, true);
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
