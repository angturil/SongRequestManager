using BeatSaverDownloader.UI;
//using CustomUI.Utilities;
using StreamCore.Utils;
using HMUI;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using IPA.Utilities;
using IPA.Loader;

namespace SongRequestManager
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
                    //_songDownloaderInstalled = Utilities.IsModInstalled("BeatSaver Downloader");

                    _songDownloaderInstalled = IPA.Loader.PluginManager.GetPlugin("BeatSaver Downloader") != null || IPA.Loader.PluginManager.Plugins.Any(p => p.Name == "BeatSaver Downloader");

                    Plugin.Log($"Song Browser installed: {_songBrowserInstalled}");
                    Plugin.Log($"Downloader installed: {_songDownloaderInstalled}");
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
            var _songBrowserUI = SongBrowser.SongBrowserApplication.Instance.GetPrivateField<SongBrowser.UI.SongBrowserUI>("_songBrowserUI");
            if (_songBrowserUI)
            {
                if (action.HasFlag(SongBrowserAction.ResetFilter))
                {
                    // if filter mode is set, clear it
                    if (_songBrowserUI.Model.Settings.filterMode != SongBrowser.DataAccess.SongFilterMode.None)
                    {
                        _songBrowserUI.InvokePrivateMethod("OnClearButtonClickEvent");
                    }
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
            if (!SongLoaderPlugin.SongLoader.AreSongsLoaded) yield break;

            if (!_standardLevelListViewController) yield break;

            SongLoaderPlugin.SongLoader.Instance.RetrieveNewSong(songFolderName);
            
            //// If beatsaver downloader is installed and songbrowser isnt, then we need to change the filter mode through it
            //if (resetFilterMode)
            //{
            //    // If song browser is installed, update/refresh it
            //    if (_songBrowserInstalled)
            //        ExecuteSongBrowserAction(SongBrowserAction.ResetFilter);
            //    // If beatsaver downloader is installed and songbrowser isnt, then we need to change the filter mode through it
                //if (_songDownloaderInstalled)
                  //  ExecuteSongDownloaderAction(SongDownloaderAction.ResetFilter);
            //}

            //// Set the row index to the previously selected song
            //if (selectOldLevel)
            //    ScrollToLevel(selectedLevelId);
        }

        public static IEnumerator RefreshSongs(bool fullRefresh = false, bool selectOldLevel = true)
        {
            if (!SongLoaderPlugin.SongLoader.AreSongsLoaded) yield break;
            if (!_standardLevelListViewController) yield break;

            // // Grab the currently selected level id so we can restore it after refreshing
           // string selectedLevelId = _standardLevelListViewController.selectedLevel?.levelID;

            // Wait until song loader is finished loading, then refresh the song list
            while (SongLoaderPlugin.SongLoader.AreSongsLoading) yield return null;
            SongLoaderPlugin.SongLoader.Instance.RefreshSongs(fullRefresh);
            while (SongLoaderPlugin.SongLoader.AreSongsLoading) yield return null;
            

            //// Set the row index to the previously selected song
            //if (selectOldLevel)
            //    ScrollToLevel(selectedLevelId);
        }

        public static int SelectCustomSongPack(bool resetFilters = true)
        {
            var levelPacksTableView = Resources.FindObjectsOfTypeAll<LevelPacksTableView>().First();
            var tableView = levelPacksTableView.GetPrivateField<TableView>("_tableView");
            
            var packsCollection = levelPacksTableView.GetPrivateField<IBeatmapLevelPackCollection>("_levelPackCollection");
            var customSongPackIndex = Array.FindIndex(packsCollection.beatmapLevelPacks, x => x.packID == "ModdedCustomMaps");

            if (customSongPackIndex != -1 && levelPacksTableView.GetPrivateField<int>("_selectedColumn") != customSongPackIndex)
            {
                tableView.SelectCellWithIdx(customSongPackIndex, true);
                tableView.ScrollToCellWithIdx(0, TableView.ScrollPositionType.Beginning, false);
                for (int i = 0; i < Mathf.FloorToInt(customSongPackIndex / 4); i++)
                    tableView.PageScrollDown();
            }

            //// If song browser is installed, update/refresh it
            if (_songBrowserInstalled)
            {
                ExecuteSongBrowserAction(SongBrowserAction.ResetFilter);
            }
            //// If beatsaver downloader is installed and songbrowser isnt, then we need to change the filter mode through it
            else if (_songDownloaderInstalled)
            {
                // get levels for selected pack
                var packWithLevels = BeatSaverDownloaderGetLevelPackWithLevels();

                // force an update to the levels
                _standardLevelListViewController.SetData(packWithLevels);
            }


            return customSongPackIndex;
        }

        private static BeatmapLevelPackSO BeatSaverDownloaderGetLevelPackWithLevels()
        {
            var levels = SongLoaderPlugin.SongLoader.CustomBeatmapLevelPackSO.beatmapLevelCollection.beatmapLevels.Cast<BeatmapLevelSO>().ToArray();
            var pack = SongLoaderPlugin.SongLoader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.First(x => x.packID == "ModdedCustomMaps");
            return BeatSaverDownloader.Misc.CustomHelpers.GetLevelPackWithLevels(levels, "Custom Songs", pack.coverImage);
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

                Plugin.Log($"Scrolling to {levelID}! Retry={isRetry}");

                var packIndex = SelectCustomSongPack();

                yield return null;

                // get the table view
                var levelsTableView = _standardLevelListViewController.GetPrivateField<LevelPackLevelsTableView>("_levelPackLevelsTableView");

                // get the table view
                var tableView = levelsTableView.GetPrivateField<TableView>("_tableView");

                // get the row number for the song we want
                var songIndex = Array.FindIndex(SongLoaderPlugin.SongLoader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks[packIndex].beatmapLevelCollection.beatmapLevels, x => x.levelID == levelID);

                // bail if song is not found, shouldn't happen
                if (songIndex >= 0)
                {
                    // if header is being shown, increment row
                    if (levelsTableView.GetPrivateField<bool>("_showLevelPackHeader"))
                    {
                        songIndex++;
                    }

                    Plugin.Log($"Selecting row {songIndex}");

                    // scroll to song
                    tableView.ScrollToCellWithIdx(songIndex, TableView.ScrollPositionType.Beginning, animated);

                    // select song, and fire the event
                    tableView.SelectCellWithIdx(songIndex, true);

                    Plugin.Log("Selected song with index " + songIndex);
                    callback?.Invoke(true);
                    yield break;
                }
            }

            if (!isRetry)
            {

                yield return SongListUtils.RefreshSongs(false, false);
                yield return ScrollToLevel(levelID, callback, animated, true);
                yield break;
            }

            var tempLevels = SongLoaderPlugin.SongLoader.CustomLevels.Where(l => l.levelID == levelID).ToArray();
            foreach (var l in tempLevels)
                SongLoaderPlugin.SongLoader.CustomLevels.Remove(l);

            Plugin.Log($"Failed to scroll to {levelID}!");
            callback?.Invoke(false);
        }
    }
}
