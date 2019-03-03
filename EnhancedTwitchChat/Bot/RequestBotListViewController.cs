//#define PRIVATE

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

        public static RequestBotListViewController Instance;

        private CustomMenu _confirmationDialog;
        private CustomViewController _confirmationViewController;
        private LevelListTableCell _songListTableCellInstance;
        private SongPreviewPlayer _songPreviewPlayer;
        private Button _playButton, _skipButton, _blacklistButton, _historyButton, _okButton, _cancelButton,_queueButton;
        private TextMeshProUGUI _warningTitle, _warningMessage;
        private HoverHint _historyHintText;
        private int _requestRow = 0;
        private int _historyRow = 0;
        private int _lastSelection = -1;
        private int _selectedRow
        {
            get { return isShowingHistory ? _historyRow : _requestRow; }
            set
            {
                if (isShowingHistory)
                    _historyRow = value;
                else
                    _requestRow = value;
            }
        }
        private Action _onConfirm;
        private bool isShowingHistory = false;
        private bool confirmDialogActive = false;

        public void Awake()
        {
            Instance = this;
        }

        static RequestBot.SongRequest currentsong = null;
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

                RectTransform container = new GameObject("CustomListContainer", typeof(RectTransform)).transform as RectTransform;
                container.SetParent(rectTransform, false);
                container.sizeDelta = new Vector2(60f, 0f);

                // History button
                _historyButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "QuitButton")), container, false);
                _historyButton.ToggleWordWrapping(false);
                (_historyButton.transform as RectTransform).anchoredPosition = new Vector2(90f, 30f);
                _historyButton.SetButtonText("History");
                _historyButton.onClick.RemoveAllListeners();
                _historyButton.onClick.AddListener(delegate ()
                {
                    isShowingHistory = !isShowingHistory;
                    UpdateRequestUI(true);
                    _lastSelection = -1;
                });
                _historyHintText = BeatSaberUI.AddHintText(_historyButton.transform as RectTransform, "");
                
                // Blacklist button
                _blacklistButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "QuitButton")), container, false);
                _blacklistButton.ToggleWordWrapping(false);
                (_blacklistButton.transform as RectTransform).anchoredPosition = new Vector2(90f, 10f);
                _blacklistButton.SetButtonText("Blacklist");
                //_blacklistButton.GetComponentInChildren<Image>().color = Color.red;
                _blacklistButton.onClick.RemoveAllListeners();
                _blacklistButton.onClick.AddListener(delegate ()
                {
                    _onConfirm = () => {
                        RequestBot.Blacklist(_selectedRow, isShowingHistory, true);
                        if(_selectedRow > 0)
                            _selectedRow--;
                    };
                    var song = SongInfoForRow(_selectedRow).song;
                    _warningTitle.text = "Blacklist Song Warning";
                    _warningMessage.text = $"Blacklisting {song["songName"].Value} by {song["authorName"].Value}\r\nDo you want to continue?";
                    confirmDialogActive = true;
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
                        currentsong = SongInfoForRow(_selectedRow);
                        RequestBot.Skip(_selectedRow);
                        if (_selectedRow > 0)
                            _selectedRow--;
                    };
                    var song = SongInfoForRow(_selectedRow).song;
                    _warningTitle.text = "Skip Song Warning";
                    _warningMessage.text = $"Skipping {song["songName"].Value} by {song["authorName"].Value}\r\nDo you want to continue?";
                    confirmDialogActive = true;
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
                    if (NumberOfRows() > 0)
                    {

                        currentsong = SongInfoForRow(_selectedRow);
                        RequestBot.played.Add(currentsong.song);
                        SetUIInteractivity(false);
                        
                        RequestBot.Process(_selectedRow, isShowingHistory);
                    }
                });
                BeatSaberUI.AddHintText(_playButton.transform as RectTransform, "Download and scroll to the currently selected request.");

                // Queue button
                _queueButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "QuitButton")), container, false);
                _queueButton.ToggleWordWrapping(false);
                _queueButton.SetButtonTextSize(3.5f);
                (_queueButton.transform as RectTransform).anchoredPosition = new Vector2(90f, -30f);
                _queueButton.SetButtonText(RequestBot.QueueOpen ? "Queue Open" : "Queue Closed");
                _queueButton.GetComponentInChildren<Image>().color = RequestBot.QueueOpen ? Color.green : Color.red; ;
                _queueButton.interactable = true;
                _queueButton.onClick.RemoveAllListeners();
                _queueButton.onClick.AddListener(delegate ()
                {
                    RequestBot.QueueOpen = !RequestBot.QueueOpen;
                    RequestBot.WriteQueueStatusToFile(RequestBot.QueueOpen ? "Queue is open." : "Queue is closed.");
                    RequestBot.Instance.QueueChatMessage(RequestBot.QueueOpen ? "Queue is open." : "Queue is closed.");
                    UpdateRequestUI();
                });
                BeatSaberUI.AddHintText(_queueButton.transform as RectTransform, "Open/Close the queue.");
            }
            base.DidActivate(firstActivation, type);
            UpdateRequestUI();
            SetUIInteractivity(true);
        }

        protected override void DidDeactivate(DeactivationType type)
        {
            base.DidDeactivate(type);
            if(!confirmDialogActive)
                isShowingHistory = false;
        }


        public void UpdateRequestUI(bool selectRowCallback = false)
        {
            _skipButton.interactable = !isShowingHistory;
            _playButton.GetComponentInChildren<Image>().color = ((isShowingHistory && RequestBot.SongRequestHistory.Count > 0) || (!isShowingHistory && RequestBot.FinalRequestQueue.Count > 0)) ? Color.green : Color.red;
            _queueButton.SetButtonText(RequestBot.QueueOpen ? "Queue Open" : "Queue Closed");
            _queueButton.GetComponentInChildren<Image>().color = RequestBot.QueueOpen ? Color.green : Color.red; ;
            _historyHintText.text = isShowingHistory ? "Go back to your current song request queue." : "View the history of song requests from the current session.";
            _historyButton.SetButtonText(isShowingHistory ? "Requests" : "History");
            _playButton.SetButtonText(isShowingHistory ? "Replay" : "Play");

            _customListTableView.ReloadData();
            
            if (NumberOfRows() > _selectedRow)
            {
                _customListTableView.SelectRow(_selectedRow, selectRowCallback);
                _customListTableView.ScrollToRow(_selectedRow, true);
            }
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
                confirmDialogActive = false;
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
                confirmDialogActive = false;
            });
            _confirmationDialog.SetMainViewController(_confirmationViewController, false);
        }

        private void DidSelectRow(TableView table, int row)
        {
            _selectedRow = row;
            if (row != _lastSelection)
            {
                CustomLevel level = CustomLevelForRow(row);
                if (level)
                    SongLoader.Instance.LoadAudioClipForLevel(level, (customLevel) => { PlayPreview(customLevel); });
                else
                    _songPreviewPlayer.CrossfadeToDefault();
                _lastSelection = row;
            }
            
        }

        private void SongLoader_SongsLoadedEvent(SongLoader arg1, List<CustomLevel> arg2)
        {
            _customListTableView?.ReloadData();
        }

        private void SetUIInteractivity(bool interactive)
        {
            _backButton.interactable = interactive;
            _playButton.interactable = interactive;
            _skipButton.interactable = interactive;
            _blacklistButton.interactable = interactive;
            _historyButton.interactable = interactive;
        }
        
        private CustomLevel CustomLevelForRow(int row)
        {
            var levels = SongLoader.CustomLevels.Where(l => l.levelID.StartsWith((SongInfoForRow(row).song["hashMd5"].Value).ToUpper()))?.ToArray();
            if (levels.Count() > 0)
                return levels[0];
            return null;
        }

        private RequestBot.SongRequest SongInfoForRow(int row)
        {
            return isShowingHistory ? RequestBot.SongRequestHistory.ElementAt(row) : RequestBot.FinalRequestQueue.ElementAt(row);
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
            return isShowingHistory ? RequestBot.SongRequestHistory.Count() : RequestBot.FinalRequestQueue.Count();
        }

        public override TableCell CellForRow(int row)
        {
            LevelListTableCell _tableCell = _customListTableView.DequeueReusableCellForIdentifier("LevelListTableCell") as LevelListTableCell;
            if (!_tableCell)
            {
                _tableCell = Instantiate(_songListTableCellInstance);
                _tableCell.reuseIdentifier = "LevelListTableCell";
            } 
            _tableCell.coverImage = null;

            RequestBot.SongRequest request = SongInfoForRow(row);
            JSONObject song = request.song;

            BeatSaberUI.AddHintText(_tableCell.transform as RectTransform, $"Requested by {request.requestor.displayName}\nStatus: {request.status.ToString()}\n\n<size=60%>Request Time: {request.requestTime.ToLocalTime()}</size>");
            _tableCell.songName = song["songName"].Value;
            _tableCell.author = song["authorName"].Value;
            if (SongLoader.AreSongsLoaded)
            {
                CustomLevel level = CustomLevelForRow(row);
                if (level)
                    _tableCell.coverImage = level.coverImage;
            }
            if (_tableCell.coverImage == null)
            {
                string url = song["coverUrl"].Value;
                _tableCell.coverImage = GetSongCoverArt(url, (sprite) => { _cachedSprites[url] = sprite; _customListTableView.ReloadData(); });
            }
            return _tableCell;
        }
    }
}
