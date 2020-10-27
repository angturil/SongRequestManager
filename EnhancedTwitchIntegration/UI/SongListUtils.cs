using HMUI;
using IPA.Utilities;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

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

        private static IEnumerator SelectCustomSongPack()
        {
            // get the select Level category view controller
            var selectLevelCategoryViewController = Resources.FindObjectsOfTypeAll<SelectLevelCategoryViewController>().First();

            Plugin.Log("1");

            // check if the selected level category is the custom category
            if (selectLevelCategoryViewController.selectedLevelCategory != SelectLevelCategoryViewController.LevelCategory.CustomSongs)
            {
                // get the icon segmented controller
                var iconSegmentedControl = selectLevelCategoryViewController.GetField<IconSegmentedControl, SelectLevelCategoryViewController>("_levelFilterCategoryIconSegmentedControl");

                Plugin.Log("2");

                // get the current level categories listed
                var levelCategoryInfos = selectLevelCategoryViewController.GetField<SelectLevelCategoryViewController.LevelCategoryInfo[], SelectLevelCategoryViewController>("_levelCategoryInfos").ToList();

                Plugin.Log("3");

                // get the index of the custom category
                var idx = levelCategoryInfos.FindIndex(lci => lci.levelCategory == SelectLevelCategoryViewController.LevelCategory.CustomSongs);

                Plugin.Log($"3 - {idx}");

                // select the custom category
                iconSegmentedControl.SelectCellWithNumber(idx);

                // ge tthe level filtering nev controller
                var levelFilteringNavigationController = Resources.FindObjectsOfTypeAll<LevelFilteringNavigationController>().First();

                // update the content, as selecting the new cell alone won't always work
                levelFilteringNavigationController.UpdateSecondChildControllerContent(SelectLevelCategoryViewController.LevelCategory.CustomSongs);

                // arbitrary wait for catch-up
                yield return new WaitForSeconds(0.1f);
            }

            Plugin.Log("4");

            // get the beatmap level collections controller
            var annotatedBeatmapLevelCollectionsViewController = Resources.FindObjectsOfTypeAll<AnnotatedBeatmapLevelCollectionsViewController>().First();

            Plugin.Log("5");

            // check if the first element is selected
            if (annotatedBeatmapLevelCollectionsViewController.selectedItemIndex != 0)
            {
                Plugin.Log("6");

                // get the level collection
                var annotatedBeatmapLevelCollectionsTableView = annotatedBeatmapLevelCollectionsViewController.GetField<AnnotatedBeatmapLevelCollectionsTableView, AnnotatedBeatmapLevelCollectionsViewController>("_annotatedBeatmapLevelCollectionsTableView");

                Plugin.Log("7");

                // select the first element
                annotatedBeatmapLevelCollectionsTableView.SelectAndScrollToCellWithIdx(0);
            }

            // arbitrary wait for catch-up
            yield return new WaitForSeconds(0.1f);

            Plugin.Log("Done");
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
                yield return SelectCustomSongPack();

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
                            var gamePlayModifierToggles = gameplayModifiersPanelController.GetField<GameplayModifierToggle[], GameplayModifiersPanelController>("_gameplayModifierToggles");
                            foreach (var gamePlayModifierToggle in gamePlayModifierToggles)
                            {
                                if (gamePlayModifierToggle.gameplayModifier.modifierNameLocalizationKey == "MODIFIER_NO_FAIL")
                                {
                                    gameplayModifiersPanelController.SetToggleValueWithGameplayModifierParams(gamePlayModifierToggle.gameplayModifier, false);
                                }
                            }
                            gameplayModifiersPanelController.RefreshTotalMultiplierAndRankUI();
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
