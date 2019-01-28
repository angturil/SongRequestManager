using CustomUI.BeatSaber;
using CustomUI.Utilities;
using EnhancedTwitchChat.UI;
using EnhancedTwitchChat.Utils;
using HMUI;
using SimpleJSON;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

namespace EnhancedTwitchChat.Bot
{
    class RequestBotListViewController : CustomListViewController
    {
        private CustomMenu _confirmationDialog;
        private CustomViewController _confirmationViewController;
        private LevelListTableCell _songListTableCellInstance;
        private SongPreviewPlayer _songPreviewPlayer;
        private Button _playButton, _skipButton, _blacklistButton, _okButton, _cancelButton;
        private TextMeshProUGUI _warningTitle, _warningMessage;
        private int _selectedRow = 0;
        private int _lastSelection = -1;
        private Action _onConfirm;
        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (firstActivation)
            {
                if (!SongLoader.AreSongsLoaded)
                    SongLoader.SongsLoadedEvent += SongLoader_SongsLoadedEvent;

                InitConfirmationDialog();

                _songListTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell"));
                _songPreviewPlayer = Resources.FindObjectsOfTypeAll<SongPreviewPlayer>().FirstOrDefault();
                DidSelectRowEvent += DidSelectRow;
                RequestBot.SongRequestQueued += (song) => RefreshTable();
                RequestBot.SongRequestDequeued += (song) => RefreshTable();

                RectTransform container = new GameObject("CustomListContainer", typeof(RectTransform)).transform as RectTransform;
                container.SetParent(rectTransform, false);
                container.sizeDelta = new Vector2(60f, 0f);

                // Blacklist button
                _blacklistButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "QuitButton")), container, false);
                _blacklistButton.ToggleWordWrapping(false);
                (_blacklistButton.transform as RectTransform).anchoredPosition = new Vector2(90f, 10f);
                _blacklistButton.SetButtonText("Blacklist");
                //_blacklistButton.GetComponentInChildren<Image>().color = Color.red;
                _blacklistButton.onClick.RemoveAllListeners();
                _blacklistButton.onClick.AddListener(delegate ()
                {
                    _onConfirm = () => { RequestBot.Blacklist(_selectedRow); };
                    var song = RequestBot.FinalRequestQueue[_selectedRow].song;
                    _warningTitle.text = "Blacklist Song Warning";
                    _warningMessage.text = $"Blacklisting {song["songName"].Value} by {song["authorName"].Value}\r\nDo you want to continue?";
                    _confirmationDialog.Present();
                });
                BeatSaberUI.AddHintText(_blacklistButton.transform as RectTransform, "Block the selected request from being queued in the future.");

                // Skip button
                _skipButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "QuitButton")), container, false);
                _skipButton.ToggleWordWrapping(false);
                (_skipButton.transform as RectTransform).anchoredPosition = new Vector2(90f, 0f);
                _skipButton.SetButtonText("Skip");
                //_skipButton.GetComponentInChildren<Image>().color = Color.yellow;
                _skipButton.onClick.RemoveAllListeners();
                _skipButton.onClick.AddListener(delegate ()
                {
                    _onConfirm = () =>
                    {
                        _lastSelection = -1;
                        RequestBot.Skip(_selectedRow);
                    };
                    var song = RequestBot.FinalRequestQueue[_selectedRow].song;
                    _warningTitle.text = "Skip Song Warning";
                    _warningMessage.text = $"Skipping {song["songName"].Value} by {song["authorName"].Value}\r\nDo you want to continue?";
                    _confirmationDialog.Present();
                });
                BeatSaberUI.AddHintText(_skipButton.transform as RectTransform, "Remove the selected request from the queue.");

                // Play button
                _playButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "QuitButton")), container, false);
                _playButton.ToggleWordWrapping(false);
                (_playButton.transform as RectTransform).anchoredPosition = new Vector2(90f, -10f);
                _playButton.SetButtonText("Play");
                _playButton.GetComponentInChildren<Image>().color = Color.green;
                _playButton.onClick.RemoveAllListeners();
                _playButton.onClick.AddListener(delegate ()
                {
                    SetUIInteractivity(false);
                    RequestBot.Process(_selectedRow);
                });
                BeatSaberUI.AddHintText(_playButton.transform as RectTransform, "Download and scroll to the currently selected request.");
            }
            base.DidActivate(firstActivation, type);
            RefreshTable();
            SetUIInteractivity(true);
        }

        private void InitConfirmationDialog()
        {
            _confirmationDialog = BeatSaberUI.CreateCustomMenu<CustomMenu>("Are you sure?");
            _confirmationViewController = BeatSaberUI.CreateViewController<CustomViewController>();

            RectTransform confirmContainer = new GameObject("CustomListContainer", typeof(RectTransform)).transform as RectTransform;
            confirmContainer.SetParent(_confirmationViewController.rectTransform, false);
            confirmContainer.sizeDelta = new Vector2(60f, 0f);

            _warningTitle = new GameObject("WarningTitle").AddComponent<TextMeshProUGUI>();
            _warningTitle.rectTransform.SetParent(confirmContainer, false);
            _warningTitle.rectTransform.anchoredPosition = new Vector2(0, 30f);
            _warningTitle.fontSize = 9f;
            _warningTitle.color = Color.red;
            _warningTitle.alignment = TextAlignmentOptions.Center;
            _warningTitle.enableWordWrapping = true;
            
            _warningMessage = new GameObject("WarningText").AddComponent<TextMeshProUGUI>();
            _warningMessage.rectTransform.SetParent(confirmContainer, false);
            _warningMessage.rectTransform.anchoredPosition = new Vector2(0, 0f);
            _warningMessage.rectTransform.sizeDelta = new Vector2(120, 1);
            _warningMessage.fontSize = 5f;
            _warningMessage.color = Color.white;
            _warningMessage.alignment = TextAlignmentOptions.Center;
            _warningMessage.enableWordWrapping = true;

            // Yes button
            _okButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "QuitButton")), confirmContainer, false);
            _okButton.ToggleWordWrapping(false);
            (_okButton.transform as RectTransform).anchoredPosition = new Vector2(43f, -30f);
            _okButton.SetButtonText("Yes");
            _okButton.onClick.RemoveAllListeners();
            _okButton.onClick.AddListener(delegate ()
            {
                _onConfirm?.Invoke();
                _confirmationDialog.Dismiss();
            });

            // No button
            _cancelButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "QuitButton")), confirmContainer, false);
            _cancelButton.ToggleWordWrapping(false);
            (_cancelButton.transform as RectTransform).anchoredPosition = new Vector2(18f, -30f); 
            _cancelButton.SetButtonText("No");
            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(delegate ()
            {
                _confirmationDialog.Dismiss();
            });
            _confirmationDialog.SetMainViewController(_confirmationViewController, false);
        }

        protected override void DidDeactivate(DeactivationType type)
        {
            base.DidDeactivate(type);
        }

        private void DidSelectRow(TableView table, int row)
        {
            _selectedRow = row;
            if (_selectedRow != _lastSelection)
            {
                CustomLevel level = CustomLevelForRow(row);
                if (level)
                    SongLoader.Instance.LoadAudioClipForLevel(level, (customLevel) => { PlayPreview(customLevel); });
                else
                    _songPreviewPlayer.CrossfadeToDefault();
            }
            _lastSelection = _selectedRow;
        }

        private void SongLoader_SongsLoadedEvent(SongLoader arg1, List<CustomLevel> arg2)
        {
            RefreshTable();
        }

        private void SetUIInteractivity(bool interactive)
        {
            _backButton.interactable = interactive;
            _playButton.interactable = interactive;
            _skipButton.interactable = interactive;
            _blacklistButton.interactable = interactive;
        }

        private void RefreshTable()
        {
            if (isActivated)
            {
                _customListTableView?.ReloadData();
                if (_customListTableView.dataSource.NumberOfRows() > 0)
                {
                    while (_customListTableView.dataSource.NumberOfRows() <= _selectedRow)
                        _selectedRow--;

                    _customListTableView.SelectRow(_selectedRow, true);
                    _customListTableView.ScrollToRow(_selectedRow, true);
                }
            }
        }

        private CustomLevel CustomLevelForRow(int row)
        {
            var levels = SongLoader.CustomLevels.Where(l => l.levelID.StartsWith(((string)SongInfoForRow(row).song["hashMd5"]).ToUpper()))?.ToArray();
            if (levels.Count() > 0)
                return levels[0];
            return null;
        }

        private RequestBot.SongRequest SongInfoForRow(int row)
        {
            return RequestBot.FinalRequestQueue.ElementAt(row);
        }

        private void PlayPreview(CustomLevel level)
        {
            _songPreviewPlayer.CrossfadeTo(level.audioClip, level.previewStartTime, level.audioClip.length - level.previewStartTime);
        }

        private static Dictionary<string, Sprite> _cachedSprites = new Dictionary<string, Sprite>();
        public static Sprite GetSongCoverArt(string url, Action<Sprite> downloadCompleted)
        {
            if (!_cachedSprites.ContainsKey(url))
            {
                RequestBot.Instance.StartCoroutine(Utilities.DownloadSpriteAsync(url, downloadCompleted));
                _cachedSprites.Add(url, UIUtilities.BlankSprite);
            }
            return _cachedSprites[url];
        }

        public override int NumberOfRows()
        {
            return RequestBot.FinalRequestQueue.Count();
        }

        public override TableCell CellForRow(int row)
        {
            LevelListTableCell _tableCell = Instantiate(_songListTableCellInstance);
            RequestBot.SongRequest request = SongInfoForRow(row);
            JSONObject song = request.song;
            BeatSaberUI.AddHintText(_tableCell.transform as RectTransform, $"Requested by {request.requestor.DisplayName}");
            _tableCell.songName = song["songName"];
            _tableCell.author = song["authorName"];
            if (SongLoader.AreSongsLoaded)
            {
                CustomLevel level = CustomLevelForRow(row);
                if (level)
                    _tableCell.coverImage = level.coverImage;
            }
            if (_tableCell.coverImage == null)
            {
                string url = song["coverUrl"];
                _tableCell.coverImage = GetSongCoverArt(url, (sprite) => { _cachedSprites[url] =  sprite; _customListTableView.ReloadData(); });
            }

            _tableCell.reuseIdentifier = "CustomListCell";
            return _tableCell;
        }
    }
}
