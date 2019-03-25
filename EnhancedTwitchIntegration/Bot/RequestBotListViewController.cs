//#define PRIVATE

using CustomUI.BeatSaber;
using EnhancedTwitchChat.Config;
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
using VRUI;
using Image = UnityEngine.UI.Image;
using System.IO;
using EnhancedTwitchChat.Chat;
using SongRequestManager.Config;

namespace SongRequestManager
{
    public class RequestBotListViewController : CustomListViewController
    {

        public class MyButton
        {
            static List<MyButton> mybuttons = new List<MyButton>();
            public static RectTransform container = null;
 
            // add buttons to container
            public static void AddButtons ()
                {

                // BUG: Prototype GARBAGE code. To be replaced as soon as the core ideas are tested

                if (container == null) return;

               // Bounding box
                float x1 = -17.5f - 20f;
                float y1 = 27.5f;
                float x2 = 0;
                //float y2 = 30f;

                float padding = 0.6f;

                var list = RequestBot.listcollection.OpenList("ViewButton.script");
                float y = y1;
                float x = x1;

                int l = 0;
                foreach (string line in list.list)
                {
                    string[] entry = line.Split(new char[] { ';' });
                    if (entry.Length > 1 && entry[0].Length > 0)
                    {
                        float width = 40f;
                        if (entry.Length > 2) float.TryParse(entry[2], out width);

                        if (x + width * 0.45f > x2)
                        {
                            y -= 5 + padding;
                            x = x1;
                        }

                        var dt = new RequestBot.DynamicText();

                        Color color = Color.blue;

                        try // This object may not exist at the time of call
                        {
                        dt.AddSong(RequestHistory.Songs[0].song);
                        if (entry.Length > 3) 
                            {
                                string deckname = entry[3].ToLower()+".deck";
                                if (RequestBot.listcollection.contains(ref deckname,RequestHistory.Songs[0].song["id"].Value)) color = Color.magenta;
                            }
                        }
                        catch
                        { }
                         
                        string ourtext = dt.Parse(entry[0]);

                        MyButton my;

                        if (l < MyButton.mybuttons.Count)
                            my = mybuttons[l];
                        else
                           my = new MyButton();

                        l++;

                        my.SetMyButton(x + width * 0.45f / 2, y, width,color, dt.Parse(entry[0]), entry[1], container);
                        x += width * 0.45f + padding;
                    }
                    else
                    {
                        y -= 7f;
                        x = x1;
                    }
                }

            }

            Button mybutton;

            public MyButton()
                {
                
                mybutton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "BuyPackButton")), container, false);
                mybuttons.Add(this);
                }
            public void SetMyButton(float x1,float y1, float width,Color color,string text,string action,RectTransform container)
                {
                TMP_Text txt = mybutton.GetComponentInChildren<TMP_Text>();

                mybutton.ToggleWordWrapping(false);
                (mybutton.transform as RectTransform).anchoredPosition = new Vector2(x1, y1);
                (mybutton.transform as RectTransform).sizeDelta = new Vector2(width, 10);
                //(mybutton.transform).localRotation = new Quaternion(0.0f, 0.2f, 0.0f, 0.0f);
                mybutton.transform.localScale = new Vector3(0.45f, 0.45f, 1.0f);
                mybutton.SetButtonTextSize(5f);
                mybutton.SetButtonText(text);
                mybutton.GetComponentInChildren<Image>().color = color;


                txt.autoSizeTextContainer = true;
                //txt.ForceMeshUpdate();
                //txt.UpdateVertexData();

                //(mybutton.transform as RectTransform).sizeDelta = txt.rectTransform.sizeDelta;

                mybutton.onClick.RemoveAllListeners();

                mybutton.onClick.AddListener(delegate ()        
                {
                    RequestBot.COMMAND.Parse(TwitchWebSocketClient.OurTwitchUser, action);
                    RequestBotListViewController.Instance.UpdateRequestUI(true);
                });
                HoverHint _MyHintText = BeatSaberUI.AddHintText(mybutton.transform as RectTransform, action);
            }
        }

        public static RequestBotListViewController Instance;

        private CustomMenu _confirmationDialog;
        private CustomViewController _confirmationViewController;

        public CustomMenu _KeyboardDialog;

        private LevelListTableCell _songListTableCellInstance;
        private SongPreviewPlayer _songPreviewPlayer;
        private Button _playButton, _skipButton, _blacklistButton, _historyButton, _okButton, _cancelButton, _queueButton;

        private TextMeshProUGUI _warningTitle, _warningMessage,_CurrentSongName,_CurrentSongName2;
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

        static public SongRequest currentsong = null;
        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (firstActivation)
            {
                if (!SongLoader.AreSongsLoaded)
                    SongLoader.SongsLoadedEvent += SongLoader_SongsLoadedEvent;

                InitConfirmationDialog();

                _songListTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(o => (o.name == "LevelListTableCell"));
                _songPreviewPlayer = Resources.FindObjectsOfTypeAll<SongPreviewPlayer>().FirstOrDefault();
                DidSelectRowEvent += DidSelectRow;

                RectTransform container = new GameObject("RequestBotContainer", typeof(RectTransform)).transform as RectTransform;
                container.SetParent(rectTransform, false);
                container.sizeDelta = new Vector2(60f, 0f);

                try
                {
                    InitKeyboardDialog();
                }
                catch (Exception ex)
                {
                    Plugin.Log(ex.ToString());
                }

#if UNRELEASED

                // BUG: This code is at an extremely early stage.
                // BUG: Need custom button colors and styles
                // BUG: Need additional modes disabling one shot buttons
                // BUG: Need to make sure the buttons are usable on older headsets



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

                MyButton.container = container; 
                MyButton.AddButtons();


#endif

                // History button
                _historyButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(o => (o.name == "QuitButton")), container, false);
                _historyButton.ToggleWordWrapping(false);
                (_historyButton.transform as RectTransform).anchoredPosition = new Vector2(90f, 30f);
                _historyButton.SetButtonText("History");
                _historyButton.onClick.RemoveAllListeners();
                _historyButton.onClick.AddListener(delegate ()
                {
                    isShowingHistory = !isShowingHistory;
                    Resources.FindObjectsOfTypeAll<VRUIScreenSystem>().First().title = isShowingHistory ? "Song Request History" : "Song Request Queue";
                    UpdateRequestUI(true);
                    _lastSelection = -1;
                });
                _historyHintText = BeatSaberUI.AddHintText(_historyButton.transform as RectTransform, "");
                
                // Blacklist button
                _blacklistButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(o => (o.name == "QuitButton")), container, false);
                _blacklistButton.ToggleWordWrapping(false);
                (_blacklistButton.transform as RectTransform).anchoredPosition = new Vector2(90f, 10f);
                _blacklistButton.SetButtonText("Blacklist");
                //_blacklistButton.GetComponentInChildren<Image>().color = Color.red;
                _blacklistButton.onClick.RemoveAllListeners();
                _blacklistButton.onClick.AddListener(delegate ()
                {
                    if (NumberOfCells() > 0)
                    {
                        _onConfirm = () =>
                        {
                            RequestBot.Blacklist(_selectedRow, isShowingHistory, true);
                            if (_selectedRow > 0)
                                _selectedRow--;
                        };
                        var song = SongInfoForRow(_selectedRow).song;
                        _warningTitle.text = "Blacklist Song Warning";
                        _warningMessage.text = $"Blacklisting {song["songName"].Value} by {song["authorName"].Value}\r\nDo you want to continue?";
                        confirmDialogActive = true;
                        _confirmationDialog.Present();
                    }
                });
                BeatSaberUI.AddHintText(_blacklistButton.transform as RectTransform, "Block the selected request from being queued in the future.");

                // Skip button
                _skipButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(o => (o.name == "QuitButton")), container, false);
                _skipButton.ToggleWordWrapping(false);
                (_skipButton.transform as RectTransform).anchoredPosition = new Vector2(90f, 0f);
                _skipButton.SetButtonText("Skip");
                //_skipButton.GetComponentInChildren<Image>().color = Color.yellow;
                _skipButton.onClick.RemoveAllListeners();
                _skipButton.onClick.AddListener(delegate ()
                {
                    if (NumberOfCells() > 0)
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
                    }
                });
                BeatSaberUI.AddHintText(_skipButton.transform as RectTransform, "Remove the selected request from the queue.");

                // Play button
                _playButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(o => (o.name == "QuitButton")), container, false);
                _playButton.ToggleWordWrapping(false);
                (_playButton.transform as RectTransform).anchoredPosition = new Vector2(90f, -10f);
                _playButton.SetButtonText("Play");
                _playButton.GetComponentInChildren<Image>().color = Color.green;
                _playButton.onClick.RemoveAllListeners();
                _playButton.onClick.AddListener(delegate ()
                {
                    if (NumberOfCells() > 0)
                    {
                        currentsong = SongInfoForRow(_selectedRow);
                        RequestBot.played.Add(currentsong.song);
                        RequestBot.WriteJSON(RequestBot.playedfilename, ref RequestBot.played);
                        
                        SetUIInteractivity(false);
                        RequestBot.Process(_selectedRow, isShowingHistory);
                    }
                });
                BeatSaberUI.AddHintText(_playButton.transform as RectTransform, "Download and scroll to the currently selected request.");

                // Queue button
                _queueButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(o => (o.name == "QuitButton")), container, false);
                _queueButton.ToggleWordWrapping(false);
                _queueButton.SetButtonTextSize(3.5f);
                (_queueButton.transform as RectTransform).anchoredPosition = new Vector2(90f, -30f);
                _queueButton.SetButtonText(RequestBotConfig.Instance.RequestQueueOpen ? "Queue Open" : "Queue Closed");
                _queueButton.GetComponentInChildren<Image>().color = RequestBotConfig.Instance.RequestQueueOpen ? Color.green : Color.red; ;
                _queueButton.interactable = true;
                _queueButton.onClick.RemoveAllListeners();
                _queueButton.onClick.AddListener(delegate ()
                {
                    RequestBotConfig.Instance.RequestQueueOpen = !RequestBotConfig.Instance.RequestQueueOpen;
                    RequestBotConfig.Instance.Save();
                    RequestBot.WriteQueueStatusToFile(RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open." : "Queue is closed.");
                    RequestBot.Instance.QueueChatMessage(RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open." : "Queue is closed.");
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
            if (!confirmDialogActive)
                isShowingHistory = false;
        }


        public void UpdateRequestUI(bool selectRowCallback = false)
        {
            _skipButton.interactable = !isShowingHistory;
            _playButton.GetComponentInChildren<Image>().color = ((isShowingHistory && RequestHistory.Songs.Count > 0) || (!isShowingHistory && RequestQueue.Songs.Count > 0)) ? Color.green : Color.red;
            _queueButton.SetButtonText(RequestBotConfig.Instance.RequestQueueOpen ? "Queue Open" : "Queue Closed");
            _queueButton.GetComponentInChildren<Image>().color = RequestBotConfig.Instance.RequestQueueOpen ? Color.green : Color.red; ;
            _historyHintText.text = isShowingHistory ? "Go back to your current song request queue." : "View the history of song requests from the current session.";
            _historyButton.SetButtonText(isShowingHistory ? "Requests" : "History");
            _playButton.SetButtonText(isShowingHistory ? "Replay" : "Play");


            #if UNRELEASED
            if (RequestHistory.Songs.Count > 0)
            {
                _CurrentSongName.text = RequestHistory.Songs[0].song["songName"].Value;
                _CurrentSongName2.text = $"{RequestHistory.Songs[0].song["authorName"].Value} ({RequestHistory.Songs[0].song["version"].Value})";

                MyButton.AddButtons();

                //_KeyboardDialog.Present(true);

            }
            #endif

            _customListTableView.ReloadData();

            if (NumberOfCells() > _selectedRow)
            {
                _customListTableView.SelectCellWithIdx(_selectedRow, selectRowCallback);
                _customListTableView.ScrollToCellWithIdx(_selectedRow, TableView.ScrollPositionType.Beginning, true);
            }
        }



        private void InitKeyboardDialog()
        {
            _KeyboardDialog.Present();
        }


            private void InitConfirmationDialog()
        {
            _confirmationDialog = BeatSaberUI.CreateCustomMenu<CustomMenu>("Are you sure?");
            _confirmationViewController = BeatSaberUI.CreateViewController<CustomViewController>();

            RectTransform confirmContainer = new GameObject("CustomListContainer", typeof(RectTransform)).transform as RectTransform;
            confirmContainer.SetParent(_confirmationViewController.rectTransform, false);
            confirmContainer.sizeDelta = new Vector2(60f, 0f);

            // Title text
            _warningTitle = BeatSaberUI.CreateText(confirmContainer, "", new Vector2(0, 30f));
            _warningTitle.fontSize = 9f;
            _warningTitle.color = Color.red;
            _warningTitle.alignment = TextAlignmentOptions.Center;
            _warningTitle.enableWordWrapping = false;

            // Warning text
            _warningMessage = BeatSaberUI.CreateText(confirmContainer, "", new Vector2(0, 0));
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

        private SongRequest SongInfoForRow(int row)
        {
            return isShowingHistory ? RequestHistory.Songs.ElementAt(row) : RequestQueue.Songs.ElementAt(row);
        }

        private void PlayPreview(CustomLevel level)
        {
            _songPreviewPlayer.CrossfadeTo(level.previewAudioClip, level.previewStartTime, level.previewDuration);
        }

        private static Dictionary<string, Sprite> _cachedSprites = new Dictionary<string, Sprite>();
        public static Sprite GetSongCoverArt(string url, Action<Sprite> downloadCompleted)
        {
            if (!_cachedSprites.ContainsKey(url))
            {
                RequestBot.Instance.StartCoroutine(Utilities.DownloadSpriteAsync(url, downloadCompleted));
                _cachedSprites.Add(url, CustomUI.Utilities.UIUtilities.BlankSprite);
            }
            return _cachedSprites[url];
        }

        public override int NumberOfCells()
        {
            return isShowingHistory ? RequestHistory.Songs.Count() : RequestQueue.Songs.Count();
        }

        public override TableCell CellForIdx(int row)
        {
            LevelListTableCell _tableCell = GetTableCell(row);


            _tableCell.GetPrivateField<Image>("_coverImage").sprite = null;

            SongRequest request = SongInfoForRow(row);

            //BeatSaberUI.AddHintText(_tableCell.transform as RectTransform, $"Requested by {request.requestor.displayName}\nStatus: {request.status.ToString()}\n\n<size=60%>Request Time: {request.requestTime.ToLocalTime()}</size>");

            var dt = new RequestBot.DynamicText().AddSong(request.song).AddUser(ref request.requestor); // Get basic fields
            dt.Add("Status", request.status.ToString());
            dt.Add("Info", (request.requestInfo != "") ? " / " + request.requestInfo : "");
            dt.Add("RequestTime", request.requestTime.ToLocalTime().ToString());

            BeatSaberUI.AddHintText(_tableCell.transform as RectTransform, dt.Parse(RequestBot.SongHintText));

            _tableCell.SetText(request.song["songName"].Value);
            _tableCell.SetSubText(request.song["authorName"].Value);
            if (SongLoader.AreSongsLoaded)
            {
                CustomLevel level = CustomLevelForRow(row);
                if (level)
                    _tableCell.SetIcon(level.coverImage);
            }
            if (_tableCell.GetPrivateField<Image>("_coverImage").sprite == null)
            {
                string url = request.song["coverUrl"].Value;
                _tableCell.SetIcon(GetSongCoverArt(url, (sprite) => { _cachedSprites[url] = sprite; _customListTableView.ReloadData(); }));
            }
            return _tableCell;
        }
    }
}