#if REQUEST_BOT

using BeatSaverDownloader.UI;
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

namespace EnhancedTwitchChat.UI
{
    class SongListUtils
    {
        private static LevelListViewController _standardLevelListViewController = null;
        private static bool _initialized = false;
        private static bool _songBrowserInstalled = false;
        private static bool _songDownloaderInstalled = false;

        private static List<IBeatmapLevel> CurrentLevels
        {
            get
            {
                return ReflectionUtil.GetPrivateField<IBeatmapLevel[]>(_standardLevelListViewController, "_levels").ToList();
            }
            set
            {
                _standardLevelListViewController.SetLevels(value.ToArray());
            }
        }

        public static void Initialize()
        {
            _standardLevelListViewController = Resources.FindObjectsOfTypeAll<LevelListViewController>().FirstOrDefault();

            if(!_initialized)
            {
                try
                {
                    _songBrowserInstalled = Utilities.IsModInstalled("Song Browser");
                    _songDownloaderInstalled = Utilities.IsModInstalled("BeatSaver Downloader");
                    _initialized = true;
                } catch (Exception e)
                {
                    Plugin.Log($"Exception {e}");
                }
            }
        }
        private enum SongBrowserAction { Refresh = 1, ResetFilter = 2 }
        private static void ExecuteSongBrowserAction(SongBrowserAction action)
        {
            var _songBrowserUI = SongBrowserApplication.Instance.GetPrivateField<SongBrowserPlugin.UI.SongBrowserUI>("_songBrowserUI");
            if (_songBrowserUI)
            {
                if (action.HasFlag(SongBrowserAction.ResetFilter))
                {
                    _songBrowserUI.Model.Settings.filterMode = SongFilterMode.None;
                    if (!action.HasFlag(SongBrowserAction.Refresh))
                        action |= SongBrowserAction.Refresh;
                }
                if (action.HasFlag(SongBrowserAction.Refresh))
                {
                    _songBrowserUI.UpdateSongList();
                    _songBrowserUI.RefreshSongList();
                }
            }
        }
        
        private enum SongDownloaderAction { ResetFilter = 1 }
        private static void ExecuteSongDownloaderAction(SongDownloaderAction action)
        {
            if(action.HasFlag(SongDownloaderAction.ResetFilter))
            {
                SongListTweaks.Instance.SetLevels(SongListTweaks.Instance.GetPrivateField<BeatmapCharacteristicSO>("_lastCharacteristic"), SongListTweaks.lastSortMode, "");
            }
        }

        public static void RemoveDuplicates()
        {
            _standardLevelListViewController.SetLevels(CurrentLevels.Distinct().ToArray());
        }
        
        public static IEnumerator RefreshSongs(bool fullRefresh = false, bool selectOldLevel = true, bool resetFilterMode = false)
        {
            if (!SongLoader.AreSongsLoaded) yield break;

            if (!_standardLevelListViewController) yield break;

            // Grab the currently selected level id so we can restore it after refreshing
            string selectedLevelId = _standardLevelListViewController.selectedLevel?.levelID;

            // Wait until song loader is finished loading, then refresh the song list
            while (SongLoader.AreSongsLoading) yield return null;
            SongLoader.Instance.RefreshSongs(fullRefresh);
            while (SongLoader.AreSongsLoading) yield return null;

            // If song browser is installed, update/refresh it
            if (_songBrowserInstalled)
                ExecuteSongBrowserAction(resetFilterMode ? SongBrowserAction.ResetFilter : SongBrowserAction.Refresh);
            // If beatsaver downloader is installed and songbrowser isnt, then we need to change the filter mode through it
            else if (resetFilterMode && _songDownloaderInstalled)
                ExecuteSongDownloaderAction(SongDownloaderAction.ResetFilter);
            
            // Set the row index to the previously selected song
            if (selectOldLevel)
                ScrollToLevel(selectedLevelId);
        }

        public static bool ScrollToLevel(string levelID)
        {
            var table = ReflectionUtil.GetPrivateField<LevelListTableView>(_standardLevelListViewController, "_levelListTableView");
            if (table)
            {
                RemoveDuplicates();

                TableView tableView = table.GetComponentInChildren<TableView>();
                tableView.ReloadData();

                var levels = CurrentLevels.Where(l => l.levelID == levelID).ToArray();
                if (levels.Length > 0)
                {
                    int row = table.RowNumberForLevelID(levelID);
                    tableView.SelectRow(row, true);
                    tableView.ScrollToRow(row, true);
                    return true;
                }
            }
            var tempLevels = SongLoader.CustomLevels.Where(l => l.levelID == levelID).ToArray();
            foreach (CustomLevel l in tempLevels)
                SongLoader.CustomLevels.Remove(l);

            Plugin.Log($"Failed to scroll to {levelID}!");
            return false;
        }
    }
}

#endif