using EnhancedTwitchChat.Utils;
using HMUI;
using SongBrowserPlugin;
using SongBrowserPlugin.DataAccess;
using SongLoaderPlugin;
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
        }

        private static void RefreshSongBrowser(bool resetFilterMode = false)
        {
            var _songBrowserUI = SongBrowserApplication.Instance.GetPrivateField<SongBrowserPlugin.UI.SongBrowserUI>("_songBrowserUI");
            if (_songBrowserUI)
            {
                if(resetFilterMode)
                    _songBrowserUI.Model.Settings.filterMode = SongFilterMode.None;
                _songBrowserUI.UpdateSongList();
                _songBrowserUI.RefreshSongList();
            }
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
            if (Utilities.IsModInstalled("Song Browser"))
                RefreshSongBrowser(resetFilterMode);
            
            // Set the row index to the previously selected song
            if (selectOldLevel)
                ScrollToLevel(selectedLevelId);
        }

        public static bool ScrollToLevel(string levelID)
        {
            var table = ReflectionUtil.GetPrivateField<LevelListTableView>(_standardLevelListViewController, "_levelListTableView");
            if (table)
            {
                TableView tableView = table.GetComponentInChildren<TableView>();
                tableView.ReloadData();

                var levels = CurrentLevels.Where(l => l.levelID == levelID).ToArray();
                if (levels.Length > 0)
                {
                    Plugin.Log("Found level!");
                    int row = table.RowNumberForLevelID(levelID);
                    tableView.SelectRow(row, true);
                    tableView.ScrollToRow(row, true);
                    Plugin.Log("Success scrolling to new song!");
                    return true;
                }
            }
            Plugin.Log($"Failed to scroll to {levelID}!");
            return false;
        }
    }
}
