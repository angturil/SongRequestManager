using BeatSaverDownloader.UI;
using StreamCore.Utils;
using HMUI;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using IPA.Utilities;
using IPA.Loader;
using SongCore;

namespace SongRequestManager
{
    class SongListUtils
    {
        private static LevelCollectionViewController _levelCollectionViewController = null;
        private static bool _initialized = false;
        //private static bool _songBrowserInstalled = false;
        //private static bool _songDownloaderInstalled = false;

        public static void Initialize()
        {
            _levelCollectionViewController = Resources.FindObjectsOfTypeAll<LevelCollectionViewController>().FirstOrDefault();

            if (!_initialized)
            {
                try
                {
                    //_songBrowserInstalled = false; // Utilities.IsModInstalled("Song Browser");
                    //_songDownloaderInstalled = false; // IPA.Loader.PluginManager.GetPlugin("BeatSaver Downloader") != null;

                    //Plugin.Log($"Song Browser installed: {_songBrowserInstalled}");
                    //Plugin.Log($"Downloader installed: {_songDownloaderInstalled}");
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
            //var _songBrowserUI = SongBrowser.SongBrowserApplication.Instance.GetField<SongBrowser.UI.SongBrowserUI, SongBrowser.SongBrowserApplication>("_songBrowserUI");
            //if (_songBrowserUI)
            //{
            //    if (action.HasFlag(SongBrowserAction.ResetFilter))
            //    {
            //        // if filter mode is set, clear it
            //        if (_songBrowserUI.Model.Settings.filterMode != SongBrowser.DataAccess.SongFilterMode.None)
            //        {
            //            _songBrowserUI.InvokePrivateMethod("OnClearButtonClickEvent");
            //        }
            //    }
            //}
        }

        //private enum SongDownloaderAction { ResetFilter = 1 }
        //private static void ExecuteSongDownloaderAction(SongDownloaderAction action)
        //{
        //    //if (action.HasFlag(SongDownloaderAction.ResetFilter))
        //    //{
        //    //    SongListTweaks.Instance.SetLevels(SortMode.Newest, "");
        //    //}
        //}

        //public static IEnumerator RetrieveNewSong(string songFolderName, bool resetFilterMode = false)
        //{
        //    //if (!SongLoaderPlugin.SongLoader.AreSongsLoaded) yield break;

        //    //if (!_standardLevelListViewController) yield break;

        //    //SongLoaderPlugin.SongLoader.Instance.RetrieveNewSong(songFolderName);

        //    yield return null;

        //    //// If beatsaver downloader is installed and songbrowser isnt, then we need to change the filter mode through it
        //    //if (resetFilterMode)
        //    //{
        //    //    // If song browser is installed, update/refresh it
        //    //    if (_songBrowserInstalled)
        //    //        ExecuteSongBrowserAction(SongBrowserAction.ResetFilter);
        //    //    // If beatsaver downloader is installed and songbrowser isnt, then we need to change the filter mode through it
        //    //if (_songDownloaderInstalled)
        //    //  ExecuteSongDownloaderAction(SongDownloaderAction.ResetFilter);
        //    //}

        //    //// Set the row index to the previously selected song
        //    //if (selectOldLevel)
        //    //    ScrollToLevel(selectedLevelId);
        //}

        public static IEnumerator RefreshSongs(bool fullRefresh = false, bool selectOldLevel = true)
        {
            // if (!SongLoaderPlugin.SongLoader.AreSongsLoaded) yield break;
            // if (!_standardLevelListViewController) yield break;

            // // // Grab the currently selected level id so we can restore it after refreshing
            //// string selectedLevelId = _standardLevelListViewController.selectedLevel?.levelID;

            // // Wait until song loader is finished loading, then refresh the song list
            // while (SongLoaderPlugin.SongLoader.AreSongsLoading) yield return null;
            // SongLoaderPlugin.SongLoader.Instance.RefreshSongs(fullRefresh);
            // while (SongLoaderPlugin.SongLoader.AreSongsLoading) yield return null;

            yield return null;

            //// Set the row index to the previously selected song
            //if (selectOldLevel)
            //    ScrollToLevel(selectedLevelId);
        }

        private static int SelectCustomSongPack()
        {
            // get the Level Filtering Nav Controller, the top bar
            var _levelFilteringNavigationController = Resources.FindObjectsOfTypeAll<LevelFilteringNavigationController>().First();

            // get the tab bar
            var _tabBarViewController = _levelFilteringNavigationController.GetField<TabBarViewController, LevelFilteringNavigationController>("_tabBarViewController");

            if (_tabBarViewController.selectedCellNumber != 3)
            {
                // select the 4th item, whichi is custom songs
                _tabBarViewController.SelectItem(3);

                // trigger a switch and reload
                _levelFilteringNavigationController.InvokeMethod<object, LevelFilteringNavigationController>("TabBarDidSwitch");
            }
            else
            {
                // get the annotated view controller
                var _annotatedBeatmapLevelCollectionsViewController = _levelFilteringNavigationController.GetField<AnnotatedBeatmapLevelCollectionsViewController, LevelFilteringNavigationController>("_annotatedBeatmapLevelCollectionsViewController");

                // check if the first element is selected (whichi is custom maps)
                if (_annotatedBeatmapLevelCollectionsViewController.selectedItemIndex != 0)
                {
                    // get the table view
                    var _playlistsTableView = _annotatedBeatmapLevelCollectionsViewController.GetField<AnnotatedBeatmapLevelCollectionsTableView, AnnotatedBeatmapLevelCollectionsViewController>("_playlistsTableView");

                    // get the tableview to select custom songs
                    var _tableView = _playlistsTableView.GetField<TableView, AnnotatedBeatmapLevelCollectionsTableView>("_tableView");
                    _tableView.ScrollToCellWithIdx(0, TableViewScroller.ScrollPositionType.Center, false);
                    _tableView.SelectCellWithIdx(0, true);
                }
            }

            // first element is custom maps
            return 0;
        }

        //public static SongCore.OverrideClasses.SongCoreCustomLevelCollection BeatSaverDownloaderGetLevelPackWithLevels()
        //{
        //    var levels = SongCore.Loader.CustomLevelsPack.beatmapLevelCollection.beatmapLevels.Cast<CustomPreviewBeatmapLevel>().ToArray();
        //    var pack = SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.First(x => x.packID == "custom_levelpack_CustomLevels");
        //    //return BeatSaverDownloader.Misc.CustomHelpers.GetLevelPackWithLevels(levels, "Custom Songs", pack.coverImage);
        //    return null;
        //}

        static bool barf(string s)
        {
            RequestBot.Instance.QueueChatMessage($"x={s}");
            return true;
        }

        public static IEnumerator ScrollToLevel(string levelID, Action<bool> callback, bool animated, bool isRetry = false)
        {
            if (_levelCollectionViewController)
            {
                Plugin.Log($"Scrolling to {levelID}! Retry={isRetry}");

                // handle if song browser is present
                if (Plugin.SongBrowserPluginPresent)
                {
                    Plugin.SongBrowserCancelFilter();
                }

                // Make sure our custom songpack is selected
                var packIndex = SelectCustomSongPack();

                yield return null;

                int songIndex = 0;

                // get the table view
                var levelsTableView = _levelCollectionViewController.GetField<LevelCollectionTableView, LevelCollectionViewController>("_levelCollectionTableView");

                //RequestBot.Instance.QueueChatMessage($"selecting song: {levelID} pack: {packIndex}");
                yield return null;

                // get the table view
                var tableView = levelsTableView.GetField<TableView, LevelCollectionTableView>("_tableView");

                // get list of beatmaps, this is pre-sorted, etc
                var beatmaps = levelsTableView.GetField<IPreviewBeatmapLevel[], LevelCollectionTableView>("_previewBeatmapLevels").ToList();

                // get the row number for the song we want
                songIndex = beatmaps.FindIndex(x => (x.levelID.Split('_')[2] == levelID));

                // bail if song is not found, shouldn't happen
                if (songIndex >= 0)
                {
                    // if header is being shown, increment row
                    if (levelsTableView.GetField<bool, LevelCollectionTableView>("_showLevelPackHeader"))
                    {
                        songIndex++;
                    }

                    Plugin.Log($"Selecting row {songIndex}");

                    // scroll to song
                    tableView.ScrollToCellWithIdx(songIndex, TableViewScroller.ScrollPositionType.Beginning, animated);

                    // select song, and fire the event
                    tableView.SelectCellWithIdx(songIndex, true);

                    Plugin.Log("Selected song with index " + songIndex);
                    callback?.Invoke(true);

                    if (RequestBotConfig.Instance.ClearNoFail)
                    {
                        try
                        {
                            // disable no fail gamepaly modifier
                            var gameplayModifiersPanelController = Resources.FindObjectsOfTypeAll<GameplayModifiersPanelController>().First();
                            gameplayModifiersPanelController.gameplayModifiers.noFail = false;
 
                            //gameplayModifiersPanelController.gameplayModifiers.ResetToDefault();

                            gameplayModifiersPanelController.Refresh();
                        }
                        catch
                        { }

                    }
                    yield break;
                }
            }

            if (!isRetry)
            {

                yield return SongListUtils.RefreshSongs(false, false);
                yield return ScrollToLevel(levelID, callback, animated, true);
                yield break;
            }

            //var tempLevels = SongLoaderPlugin.SongLoader.CustomLevels.Where(l => l.levelID == levelID).ToArray();
            //foreach (var l in tempLevels)
            //    SongLoaderPlugin.SongLoader.CustomLevels.Remove(l);

            Plugin.Log($"Failed to scroll to {levelID}!");
            callback?.Invoke(false);
        }
    }
}
