using HMUI;
using IPA.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SongRequestManager
{
    class SongListUtils
    {
        private static LevelCollectionViewController _levelCollectionViewController;
        private static bool _initialized = false;
        private static bool _pre120 = false;

        public static void Initialize()
        {
            _levelCollectionViewController = Resources.FindObjectsOfTypeAll<LevelCollectionViewController>().FirstOrDefault();
            
            _pre120 = IPA.Utilities.UnityGame.GameVersion.SemverValue.Minor < 20;
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
            if (selectLevelCategoryViewController.selectedLevelCategory != SelectLevelCategoryViewController.LevelCategory.All)
            {
                // get the icon segmented controller
                var iconSegmentedControl = selectLevelCategoryViewController.GetField<IconSegmentedControl, SelectLevelCategoryViewController>("_levelFilterCategoryIconSegmentedControl");

                // get the current level categories listed
                var levelCategoryInfos = selectLevelCategoryViewController.GetField<SelectLevelCategoryViewController.LevelCategoryInfo[], SelectLevelCategoryViewController>("_levelCategoryInfos").ToList();

                // get the index of the custom category
                var idx = levelCategoryInfos.FindIndex(lci => lci.levelCategory == SelectLevelCategoryViewController.LevelCategory.All);

                // select the custom category
                iconSegmentedControl.SelectCellWithNumber(idx);

                // since the select event is not bubbled, force it
                selectLevelCategoryViewController.LevelFilterCategoryIconSegmentedControlDidSelectCell(iconSegmentedControl, idx);
            }

            // Clear currently possibly applied filters
            var levelSearchViewController = Resources.FindObjectsOfTypeAll<LevelSearchViewController>().FirstOrDefault();
            levelSearchViewController?.ResetCurrentFilterParams();

            // get the level filtering nev controller
            var levelFilteringNavigationController = Resources.FindObjectsOfTypeAll<LevelFilteringNavigationController>().First();

            // update custom songs
            levelFilteringNavigationController.UpdateCustomSongs();

            // arbitrary wait for catch-up
            yield return 0;
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

                int songIndex = 0;

                // get the table view
                var levelsTableView = _levelCollectionViewController.GetField<LevelCollectionTableView, LevelCollectionViewController>("_levelCollectionTableView");

                //RequestBot.Instance.QueueChatMessage($"selecting song: {levelID} pack: {packIndex}");

                // get the table view
                var tableView = levelsTableView.GetField<TableView, LevelCollectionTableView>("_tableView");

                // get list of beatmaps, this is pre-sorted, etc
                List<IPreviewBeatmapLevel> beatmaps;
                if(_pre120)
                    beatmaps = levelsTableView.GetField<IPreviewBeatmapLevel[], LevelCollectionTableView>("_previewBeatmapLevels").ToList();
                else
                    beatmaps = levelsTableView.GetField<IReadOnlyList<IPreviewBeatmapLevel>, LevelCollectionTableView>("_previewBeatmapLevels").ToList();
                
                // get the row number for the song we want
                songIndex = beatmaps.FindIndex(x => (x.levelID.StartsWith("custom_level_" + levelID)));

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
                    tableView.ScrollToCellWithIdx(songIndex, TableView.ScrollPositionType.Beginning, animated);

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
