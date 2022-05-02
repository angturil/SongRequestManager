using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using SongRequestManager.UI;
using IPA.Utilities;
using BeatSaberMarkupLanguage;
using System.Collections.Concurrent;

namespace SongRequestManager
{

    public class RequestBotListViewController : ViewController, TableView.IDataSource
    {
        public static RequestBotListViewController Instance;

        private bool confirmDialogActive = false;

        // ui elements
        private Button _pageUpButton;
        private Button _pageDownButton;
        private Button _playButton;
        private Button _skipButton;
        private Button _blacklistButton;
        private Button _historyButton;
        private Button _queueButton;
        private Button _websocketConnectButton;

        private TableView _songListTableView;
        private LevelListTableCell _requestListTableCellInstance;

        private TextMeshProUGUI _CurrentSongName;
        private TextMeshProUGUI _CurrentSongName2;

        private HoverHint _historyHintText;

        private SongPreviewPlayer _songPreviewPlayer;

        private int _requestRow = 0;
        private int _historyRow = 0;
        private int _lastSelection = -1;

        private bool isShowingHistory = false;

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

        private KEYBOARD CenterKeys;

        string SONGLISTKEY = @"
[blacklist last]/0'!block/current%CR%'

[fun +]/25'!fun/selected/toggle%CR%' [hard +]/25'!hard/selected/toggle%CR%'
[dance +]/25'!dance/selected/toggle%CR%' [chill +]/25'!chill/selected/toggle%CR%'
[brutal +]/25'!brutal/selected/toggle%CR%' 

[Random song!]/0'!decklist draw%CR%'";

        public void Awake()
        {
            Instance = this;
        }

        public void ColorDeckButtons(KEYBOARD kb, Color basecolor, Color Present, bool setSprite = false)
        {
            if (RequestHistory.Songs.Count == 0) return;
            foreach (KEYBOARD.KEY key in kb.keys)
            {
                foreach (var item in RequestBot.deck)
                {
                    string search = $"!{item.Key}/selected/toggle";
                    if (key.value.StartsWith(search))
                    {
                        string deckname = item.Key.ToLower() + ".deck";
                        Color color = (RequestBot.listcollection.contains(ref deckname, CurrentlySelectedSong().song["id"].Value)) ? Present : basecolor;

                        key.mybutton.HighlightDeckButton(color);
                    }
                }
            }
        }

        static public SongRequest currentsong = null;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            
            if (firstActivation)
            {
                if (!SongCore.Loader.AreSongsLoaded)
                {
                    SongCore.Loader.SongsLoadedEvent += SongLoader_SongsLoadedEvent;
                }

                Plugin.Log("DidActivate 001");

                // get table cell instance
                _requestListTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First((LevelListTableCell x) => x.name == "LevelListTableCell");

                // initialize Yes/No modal
                YesNoModal.instance.Setup();

                Plugin.Log("DidActivate 002");

                _songPreviewPlayer = Resources.FindObjectsOfTypeAll<SongPreviewPlayer>().FirstOrDefault();

                RectTransform container = new GameObject("RequestBotContainer", typeof(RectTransform)).transform as RectTransform;
                container.SetParent(rectTransform, false);

                #region TableView Setup and Initialization
                var go = new GameObject("SongRequestTableView", typeof(RectTransform));
                go.SetActive(false);

                go.AddComponent<ScrollRect>();
                go.AddComponent<Touchable>();
                go.AddComponent<EventSystemListener>();

                ScrollView scrollView = go.AddComponent<ScrollView>();

                _songListTableView = go.AddComponent<TableView>();
                go.AddComponent<RectMask2D>();
                _songListTableView.transform.SetParent(container, false);

                _songListTableView.SetField("_preallocatedCells", new TableView.CellsGroup[0]);
                _songListTableView.SetField("_isInitialized", false);
                _songListTableView.SetField("_scrollView", scrollView);

                var viewport = new GameObject("Viewport").AddComponent<RectTransform>();
                viewport.SetParent(go.GetComponent<RectTransform>(), false);
                go.GetComponent<ScrollRect>().viewport = viewport;
                (viewport.transform as RectTransform).sizeDelta = new Vector2(70f, 70f);

                RectTransform content = new GameObject("Content").AddComponent<RectTransform>();
                content.SetParent(viewport, false);

                scrollView.SetField("_contentRectTransform", content);
                scrollView.SetField("_viewport", viewport);

                _songListTableView.SetDataSource(this, false);

                _songListTableView.LazyInit();

                go.SetActive(true);

                (_songListTableView.transform as RectTransform).sizeDelta = new Vector2(70f, 70f);
                (_songListTableView.transform as RectTransform).anchoredPosition = new Vector2(3f, 0f);

                _songListTableView.didSelectCellWithIdxEvent += DidSelectRow;

                _pageUpButton = UIHelper.CreateUIButton("SRMPageUpButton",
                    container,
                    "PracticeButton",
                    new Vector2(0f, 38.5f),
                    new Vector2(15f, 7f),
                    () => { scrollView.PageUpButtonPressed(); },
                    "˄");
                Destroy(_pageUpButton.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "Underline"));

                _pageDownButton = UIHelper.CreateUIButton("SRMPageDownButton",
                    container,
                    "PracticeButton",
                    new Vector2(0f, -38.5f),
                    new Vector2(15f, 7f),
                    () => { scrollView.PageDownButtonPressed(); },
                    "˅");
                Destroy(_pageDownButton.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "Underline"));
                #endregion

                CenterKeys = new KEYBOARD(container, "", false, -15, 15);

#if UNRELEASED
                // BUG: Need additional modes disabling one shot buttons
                // BUG: Need to make sure the buttons are usable on older headsets

                Plugin.Log("DidActivate 005");

                _CurrentSongName = BeatSaberUI.CreateText(container, "", new Vector2(-35, 37f));
                _CurrentSongName.fontSize = 3f;
                _CurrentSongName.color = Color.cyan;
                _CurrentSongName.alignment = TextAlignmentOptions.Left;
                _CurrentSongName.enableWordWrapping = false;
                _CurrentSongName.text = "";

                _CurrentSongName2 = BeatSaberUI.CreateText(container, "", new Vector2(-35, 34f));
                _CurrentSongName2.fontSize = 3f;
                _CurrentSongName2.color = Color.cyan;
                _CurrentSongName2.alignment = TextAlignmentOptions.Left;
                _CurrentSongName2.enableWordWrapping = false;
                _CurrentSongName2.text = "";

                //CenterKeys.AddKeys(SONGLISTKEY);
                if (!RequestBot.AddKeyboard(CenterKeys, "mainpanel.kbd"))
                {
                    CenterKeys.AddKeys(SONGLISTKEY);
                }

                ColorDeckButtons(CenterKeys, Color.white, Color.magenta);
#endif

                RequestBot.AddKeyboard(CenterKeys, "CenterPanel.kbd");

                CenterKeys.DefaultActions();

                #region History button
                // History button
                _historyButton = UIHelper.CreateUIButton("SRMHistory", container, "PracticeButton", new Vector2(53f, 30f),
                    new Vector2(25f, 15f),
                    () =>
                    {
                        isShowingHistory = !isShowingHistory;
                        RequestBot.SetTitle(isShowingHistory ? "Song Request History" : "Song Request Queue");
                        if (NumberOfCells() > 0)
                        {
                            _songListTableView.ScrollToCellWithIdx(0, TableView.ScrollPositionType.Beginning, false);
                            _songListTableView.SelectCellWithIdx(0);
                            _selectedRow = 0;
                        }
                        else
                        {
                            _selectedRow = -1;
                        }
                        UpdateRequestUI(true);
                        SetUIInteractivity();
                        _lastSelection = -1;
                    }, "History");

                _historyButton.ToggleWordWrapping(false);
                _historyHintText = UIHelper.AddHintText(_historyButton.transform as RectTransform, "");
                #endregion

                #region Blacklist button
                // Blacklist button
                _blacklistButton = UIHelper.CreateUIButton("SRMBlacklist", container, "PracticeButton", new Vector2(53f, 10f),
                    new Vector2(25f, 15f),
                    () =>
                    {
                        if (NumberOfCells() > 0)
                        {
                            void _onConfirm()
                            {
                                RequestBot.Blacklist(_selectedRow, isShowingHistory, true);
                                if (_selectedRow > 0)
                                    _selectedRow--;
                                confirmDialogActive = false;
                            }

                            // get song
                            var song = SongInfoForRow(_selectedRow).song;

                            // indicate dialog is active
                            confirmDialogActive = true;

                            // show dialog
                            YesNoModal.instance.ShowDialog("Blacklist Song Warning", $"Blacklisting {song["songName"].Value} by {song["authorName"].Value}\r\nDo you want to continue?", _onConfirm, () => { confirmDialogActive = false; });
                        }
                    }, "Blacklist");

                _blacklistButton.ToggleWordWrapping(false);
                UIHelper.AddHintText(_blacklistButton.transform as RectTransform, "Block the selected request from being queued in the future.");
                #endregion

                #region Skip button
                // Skip button
                _skipButton = UIHelper.CreateUIButton("SRMSkip", container, "PracticeButton", new Vector2(53f, 0f),
                    new Vector2(25f, 15f),
                    () =>
                    {
                        if (NumberOfCells() > 0)
                        {
                            // get song
                            var song = SongInfoForRow(_selectedRow).song;

                            void _onConfirm()
                            {
                                // get selected song
                                currentsong = SongInfoForRow(_selectedRow);

                                // skip it
                                RequestBot.Skip(_selectedRow);

                                // select previous song if not first song
                                if (_selectedRow > 0)
                                {
                                    _selectedRow--;
                                }

                                // indicate dialog is no longer active
                                confirmDialogActive = false;
                            }

                            // indicate dialog is active
                            confirmDialogActive = true;

                            // show dialog
                            YesNoModal.instance.ShowDialog("Skip Song Warning", $"Skipping {song["songName"].Value} by {song["authorName"].Value}\r\nDo you want to continue?", _onConfirm, () => { confirmDialogActive = false; });
                        }
                    }, "Skip");

                _skipButton.ToggleWordWrapping(false);
                UIHelper.AddHintText(_skipButton.transform as RectTransform, "Remove the selected request from the queue.");
                #endregion

                #region Play button
                // Play button
                _playButton = UIHelper.CreateUIButton("SRMPlay", container, "ActionButton", new Vector2(53f, -10f),
                    new Vector2(25f, 15f),
                    () =>
                    {
                        if (NumberOfCells() > 0)
                        {
                            currentsong = SongInfoForRow(_selectedRow);
                            RequestBot.played.Add(currentsong.song);
                            RequestBot.WriteJSON(RequestBot.playedfilename, ref RequestBot.played);

                            SetUIInteractivity(false);
                            RequestBot.Process(_selectedRow, isShowingHistory);
                            _selectedRow = -1;
                        }
                    }, "Play");

                ((RectTransform)_playButton.transform).localScale = Vector3.one;
                _playButton.GetComponent<NoTransitionsButton>().enabled = true;

                _playButton.ToggleWordWrapping(false);
                _playButton.interactable = ((isShowingHistory && RequestHistory.Songs.Count > 0) || (!isShowingHistory && RequestQueue.Songs.Count > 0));
                UIHelper.AddHintText(_playButton.transform as RectTransform, "Download and scroll to the currently selected request.");
                #endregion

                #region Queue button
                // Queue button
                _queueButton = UIHelper.CreateUIButton("SRMQueue", container, "PracticeButton", new Vector2(53f, -30f),
                    new Vector2(25f, 15f),
                    () =>
                    {
                        RequestBotConfig.Instance.RequestQueueOpen = !RequestBotConfig.Instance.RequestQueueOpen;
                        RequestBotConfig.Instance.Save();
                        RequestBot.WriteQueueStatusToFile(RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open." : "Queue is closed.");
                        RequestBot.Instance.QueueChatMessage(RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open." : "Queue is closed.");
                        UpdateRequestUI();
                    }, RequestBotConfig.Instance.RequestQueueOpen ? "Queue Open" : "Queue Closed");

                _queueButton.ToggleWordWrapping(true);
                _queueButton.SetButtonUnderlineColor(RequestBotConfig.Instance.RequestQueueOpen ? Color.green : Color.red);
                _queueButton.SetButtonTextSize(3.5f);
                UIHelper.AddHintText(_queueButton.transform as RectTransform, "Open/Close the queue.");
                #endregion

                #region Websocket Connect Button
                // Websocket Connect button
                _websocketConnectButton = UIHelper.CreateUIButton("WSConnect", container, "PracticeButton",
                    new Vector2(53f, -20f),
                    new Vector2(25f, 15f),
                    () =>
                    {
                        ChatHandler.WebsocketHandlerConnect();
                    }, "Connect WS");

                _websocketConnectButton.ToggleWordWrapping(true);
                _websocketConnectButton.SetButtonUnderlineColor(Color.red);
                _websocketConnectButton.SetButtonTextSize(3.5f);
                UIHelper.AddHintText(_websocketConnectButton.transform as RectTransform, "Connects the Websocket");
            
                #endregion
                
                // Set default RequestFlowCoordinator title
                RequestBot.SetTitle(isShowingHistory ? "Song Request History" : "Song Request Queue");
            }
            
            
            
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            if (addedToHierarchy)
            {
                _selectedRow = -1;
                _songListTableView.ClearSelection();
            }

            UpdateRequestUI();
            SetUIInteractivity(true);
        }

        protected override void DidDeactivate(bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidDeactivate(addedToHierarchy, screenSystemEnabling);
            if (!confirmDialogActive)
            {
                isShowingHistory = false;
            }
        }

        public SongRequest CurrentlySelectedSong()
        {
            var currentsong = RequestHistory.Songs[0];

            if (_selectedRow != -1 && NumberOfCells() > _selectedRow)
            {
                currentsong = SongInfoForRow(_selectedRow);
            }
            return currentsong;
        }

        public void UpdateSelectSongInfo()
        {
#if UNRELEASED
            if (RequestHistory.Songs.Count > 0)
            {
                var currentsong = CurrentlySelectedSong();

                _CurrentSongName.text = currentsong.song["songName"].Value;
                _CurrentSongName2.text = $"{currentsong.song["authorName"].Value} ({currentsong.song["version"].Value})";

                ColorDeckButtons(CenterKeys, Color.white, Color.magenta);
            }
#endif
        }

        public void UpdateRequestUI(bool selectRowCallback = false)
        {
            _playButton.interactable = ((isShowingHistory && RequestHistory.Songs.Count > 0) || (!isShowingHistory && RequestQueue.Songs.Count > 0));

            _queueButton.SetButtonText(RequestBotConfig.Instance.RequestQueueOpen ? "Queue Open" : "Queue Closed");
            _queueButton.SetButtonUnderlineColor(RequestBotConfig.Instance.RequestQueueOpen ? Color.green : Color.red);

            _historyHintText.text = isShowingHistory ? "Go back to your current song request queue." : "View the history of song requests from the current session.";
            _historyButton.SetButtonText(isShowingHistory ? "Requests" : "History");
            _playButton.SetButtonText(isShowingHistory ? "Replay" : "Play");

            _websocketConnectButton.gameObject.SetActive(!ChatHandler.WebsocketHandlerConnected() && RequestBotConfig.Instance.WebsocketEnabled);
            
            UpdateSelectSongInfo();

            _songListTableView.ReloadData();

            if (_selectedRow == -1) return;

            if (NumberOfCells() > _selectedRow)
            {
                _songListTableView.SelectCellWithIdx(_selectedRow, selectRowCallback);
                _songListTableView.ScrollToCellWithIdx(_selectedRow, TableView.ScrollPositionType.Beginning, true);
            }
        }

        public void UpdateWebsocketConnectButton(bool active)
        {
            _websocketConnectButton.gameObject.SetActive(active);
        }

        private void DidSelectRow(TableView table, int row)
        {
            _selectedRow = row;
            if (row != _lastSelection)
            {
                _lastSelection = row;
            }

            // if not in history, disable play button if request is a challenge
            if (!isShowingHistory)
            {
                var request = SongInfoForRow(row);
                var isChallenge = request.requestInfo.IndexOf("!challenge", StringComparison.OrdinalIgnoreCase) >= 0;
                _playButton.interactable = !isChallenge;
            }

            UpdateSelectSongInfo();

            SetUIInteractivity();
        }

        private void SongLoader_SongsLoadedEvent(SongCore.Loader arg1, ConcurrentDictionary <string,CustomPreviewBeatmapLevel> arg2)
        {
            _songListTableView?.ReloadData();
        }

        private List<SongRequest> Songs => isShowingHistory ? RequestHistory.Songs : RequestQueue.Songs;

        /// <summary>
        /// Alter the state of the buttons based on selection
        /// </summary>
        /// <param name="interactive">Set to false to force disable all buttons, true to auto enable buttons based on states</param>
        public void SetUIInteractivity(bool interactive = true)
        {
            var toggled = interactive;

            if (_selectedRow >= (isShowingHistory ? RequestHistory.Songs : RequestQueue.Songs).Count()) _selectedRow = -1;

            if (NumberOfCells() == 0 || _selectedRow == -1 || _selectedRow >= Songs.Count())
            {
                Plugin.Log("Nothing selected, or empty list, buttons should be off");
                toggled = false;
            }

            var playButtonEnabled = toggled;
            if (toggled && !isShowingHistory)
            {
                var request = SongInfoForRow(_selectedRow);
                var isChallenge = request.requestInfo.IndexOf("!challenge", StringComparison.OrdinalIgnoreCase) >= 0;
                playButtonEnabled = isChallenge ? false : toggled;
            }

            _playButton.interactable = playButtonEnabled;

            var skipButtonEnabled = toggled;
            if (toggled && isShowingHistory)
            {
                skipButtonEnabled = false;
            }
            _skipButton.interactable = skipButtonEnabled;

            _blacklistButton.interactable = toggled;

            // history button can be enabled even if others are disabled
            _historyButton.interactable = true;

            _playButton.interactable = interactive;
            _skipButton.interactable = interactive;
            _blacklistButton.interactable = interactive;
        }

        private CustomPreviewBeatmapLevel CustomLevelForRow(int row)
        {
            // get level id from hash
            var levelIds = SongCore.Collections.levelIDsForHash(SongInfoForRow(row).song["hash"]);
            if (levelIds.Count == 0) return null;
            
            // lookup song from level id
            return SongCore.Loader.CustomLevels.FirstOrDefault(s => string.Equals(s.Value.levelID, levelIds.First(), StringComparison.OrdinalIgnoreCase)).Value ?? null;
        }

        private SongRequest SongInfoForRow(int row)
        {
            return isShowingHistory ? RequestHistory.Songs.ElementAt(row) : RequestQueue.Songs.ElementAt(row);
        }

        private void PlayPreview(CustomPreviewBeatmapLevel level)
        {
            //_songPreviewPlayer.CrossfadeTo(level.previewAudioClip, level.previewStartTime, level.previewDuration);
        }

        private static Dictionary<string, Texture2D> _cachedTextures = new Dictionary<string, Texture2D>();

        #region TableView.IDataSource interface
        public float CellSize() { return 10f; }

        public int NumberOfCells()
        {
            return isShowingHistory ? RequestHistory.Songs.Count() : RequestQueue.Songs.Count();
        }

        public TableCell CellForIdx(TableView tableView, int row)
        {
            LevelListTableCell _tableCell = Instantiate(_requestListTableCellInstance);
            _tableCell.reuseIdentifier = "RequestBotSongCell";
            _tableCell.SetField("_notOwned", false);

            SongRequest request = SongInfoForRow(row);
            SetDataFromLevelAsync(request, _tableCell, row);

            return _tableCell;
        }
        #endregion

        private async void SetDataFromLevelAsync(SongRequest request, LevelListTableCell _tableCell, int row)
        {
            var favouritesBadge = _tableCell.GetField<Image, LevelListTableCell>("_favoritesBadgeImage");
            favouritesBadge.enabled = false;

            var highlight = (request.requestInfo.Length > 0) && (request.requestInfo[0] == '!');

            var msg = highlight ? "MSG" : "";

            var hasMessage = (request.requestInfo.Length > 0) && (request.requestInfo[0] == '!');
            var isChallenge = request.requestInfo.IndexOf("!challenge", StringComparison.OrdinalIgnoreCase) >= 0;

            var pp = "";
            var ppvalue = request.song["pp"].AsInt;
            if (ppvalue > 0) pp = $" {ppvalue} PP";

            var dt = new RequestBot.DynamicText().AddSong(request.song).AddUser(ref request.requestor); // Get basic fields
            dt.Add("Status", request.status.ToString());
            dt.Add("Info", (request.requestInfo != "") ? " / " + request.requestInfo : "");
            dt.Add("RequestTime", request.requestTime.ToLocalTime().ToString("hh:mm"));

            var songDurationText = _tableCell.GetField<TextMeshProUGUI, LevelListTableCell>("_songDurationText");
            songDurationText.text = request.song["songlength"].Value;

            var songBpm = _tableCell.GetField<TextMeshProUGUI, LevelListTableCell>("_songBpmText");
            if(!request.requestor.IsModerator && !request.requestor.IsVip)
                (songBpm.transform as RectTransform).anchoredPosition = new Vector2(-2.5f, -1.8f);
            (songBpm.transform as RectTransform).sizeDelta += new Vector2(15f, 0f);

            
            var k = new List<string>();
            if (hasMessage) k.Add("MSG");
            if (isChallenge) k.Add("VS");
            k.Add(request.song["id"]);
            songBpm.text = string.Join(" - ", k);


            var songBmpIcon = _tableCell.GetComponentsInChildren<Image>().LastOrDefault(c => string.Equals(c.name, "BpmIcon", StringComparison.OrdinalIgnoreCase));
            if (songBmpIcon != null)
            {
                songBmpIcon.color = request.requestor.roleColor;
                if(!request.requestor.IsModerator && !request.requestor.IsVip)
                    Destroy(songBmpIcon);
            }

            var songName = _tableCell.GetField<TextMeshProUGUI, LevelListTableCell>("_songNameText");
            songName.richText = true;
            songName.text = $"{request.song["songName"].Value} <size=50%>{RequestBot.GetRating(ref request.song)} <color=#3fff3f>{pp}</color></size>";

            var author = _tableCell.GetField<TextMeshProUGUI, LevelListTableCell>("_songAuthorText");
            author.richText = true;
            author.text = dt.Parse(RequestBot.QueueListRow2);

            var image = _tableCell.GetField<Image, LevelListTableCell>("_coverImage");
            var imageSet = false;

            if (SongCore.Loader.AreSongsLoaded)
            {
                var level = CustomLevelForRow(row);
                if (level != null)
                {
                    // set image from song's cover image
                    var sprite = await level.GetCoverImageAsync(System.Threading.CancellationToken.None);
                    image.sprite = sprite;
                    imageSet = true;
                }
            }

            if (!imageSet)
            {
                var url = request.song["coverURL"].Value;

                if (!_cachedTextures.TryGetValue(url, out var tex))
                {
                    var b = await Plugin.WebClient.DownloadImage($"{url}", System.Threading.CancellationToken.None);

                    tex = new Texture2D(2, 2);
                    tex.LoadImage(b);

                    try
                    {
                        _cachedTextures.Add(url, tex);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                image.sprite = Base64Sprites.Texture2DToSprite(tex);
            }

            UIHelper.AddHintText(_tableCell.transform as RectTransform, dt.Parse(RequestBot.SongHintText));
        }
    }
}
