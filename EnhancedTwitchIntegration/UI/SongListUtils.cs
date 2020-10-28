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
        private static LevelCollectionViewController _levelCollectionViewController;
        private static bool _initialized = false;

        public static void Initialize()
        {
            _levelCollectionViewController = Resources.FindObjectsOfTypeAll<LevelCollectionViewController>().FirstOrDefault();

            if (!_initialized)
            {
                try
                {
                    _initialized = true;
                }
                catch (Exception e)
                {
                    Plugin.Log($"Exception {e}");
                }
            }
        }

        private static IEnumerator SelectCustomSongPack()
        {
            // get the select Level category view controller
            var selectLevelCategoryViewController = Resources.FindObjectsOfTypeAll<SelectLevelCategoryViewController>().First();

            // check if the selected level category is the custom category
            if (selectLevelCategoryViewController.selectedLevelCategory != SelectLevelCategoryViewController.LevelCategory.CustomSongs)
            {
                // get the icon segmented controller
                var iconSegmentedControl = selectLevelCategoryViewController.GetField<IconSegmentedControl, SelectLevelCategoryViewController>("_levelFilterCategoryIconSegmentedControl");

                // get the current level categories listed
                var levelCategoryInfos = selectLevelCategoryViewController.GetField<SelectLevelCategoryViewController.LevelCategoryInfo[], SelectLevelCategoryViewController>("_levelCategoryInfos").ToList();

                // get the index of the custom category
                var idx = levelCategoryInfos.FindIndex(lci => lci.levelCategory == SelectLevelCategoryViewController.LevelCategory.CustomSongs);

                // select the custom category
                iconSegmentedControl.SelectCellWithNumber(idx);
            }

            // get the level filtering nev controller
            var levelFilteringNavigationController = Resources.FindObjectsOfTypeAll<LevelFilteringNavigationController>().First();

            // update custom songs
            levelFilteringNavigationController.UpdateCustomSongs();

            // arbitrary wait for catch-up
            yield return new WaitForSeconds(0.1f);
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
                yield return ScrollToLevel(levelID, callback, animated, true);
                yield break;
            }

            Plugin.Log($"Failed to scroll to {levelID}!");
            callback?.Invoke(false);
        }
    }
}
