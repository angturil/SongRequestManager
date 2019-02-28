using CustomUI.BeatSaber;
using EnhancedTwitchChat.Chat;
using EnhancedTwitchChat.Textures;
using EnhancedTwitchChat.UI;
using EnhancedTwitchChat.Utils;
using HMUI;
using SimpleJSON;
using SongBrowserPlugin;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using VRUI;
using Image = UnityEngine.UI.Image;
using Toggle = UnityEngine.UI.Toggle;

namespace EnhancedTwitchChat.Bot
{
    public class RequestBot : MonoBehaviour
    {
        [Flags]
        public enum RequestStatus
        {
            Invalid,
            Queued,
            Blacklisted,
            Skipped,
            Played
        }

        public class SongRequest
        {
            public JSONObject song;
            public TwitchUser requestor;
            public DateTime requestTime;
            public RequestStatus status;
            public SongRequest(JSONObject song, TwitchUser requestor, DateTime requestTime, RequestStatus status = RequestStatus.Invalid)
            {
                this.song = song;
                this.requestor = requestor;
                this.status = status;
                this.requestTime = requestTime;
            }
        }

        public class RequestInfo
        {
            public TwitchUser requestor;
            public string request;
            public bool isBeatSaverId;
            public bool isPersistent = false;
            public DateTime requestTime;
            public RequestInfo(TwitchUser requestor, string request, DateTime requestTime, bool isBeatSaverId)
            {
                this.requestor = requestor;
                this.request = request;
                this.isBeatSaverId = isBeatSaverId;
                this.requestTime = requestTime;
            }
        }

        class RequestUserTracker
        {
            public int numRequests = 0;
            public DateTime resetTime = DateTime.Now;
        }
        
        private static readonly Regex _digitRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _beatSaverRegex = new Regex("^[0-9]+-[0-9]+$", RegexOptions.Compiled);

        public static RequestBot Instance;
        public static ConcurrentQueue<RequestInfo> UnverifiedRequestQueue = new ConcurrentQueue<RequestInfo>();
        public static List<SongRequest> FinalRequestQueue = new List<SongRequest>();
        public static List<SongRequest> SongRequestHistory = new List<SongRequest>();
        public static Action<JSONObject> SongRequestQueued;
        public static Action<JSONObject> SongRequestDequeued;
        private static Button _requestButton;
        private static bool _checkingQueue = false;

        private static FlowCoordinator _levelSelectionFlowCoordinator;
        private static DismissableNavigationController _levelSelectionNavigationController;
        private static Queue<string> _botMessageQueue = new Queue<string>();
        private static Dictionary<string, Action<TwitchUser, string>> Commands = new Dictionary<string, Action<TwitchUser, string>>();

        private static List<string> _songBlacklist = new List<string>();
        private static List<string> _persistentRequestQueue = new List<string>();
        private static CustomMenu _songRequestMenu = null;
        private static RequestBotListViewController _songRequestListViewController = null;

        public static void OnLoad()
        {
            _levelSelectionFlowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
            if (_levelSelectionFlowCoordinator)
                _levelSelectionNavigationController = _levelSelectionFlowCoordinator.GetPrivateField<DismissableNavigationController>("_navigationController");

            if (_levelSelectionNavigationController)
            {
                _requestButton = BeatSaberUI.CreateUIButton(_levelSelectionNavigationController.rectTransform, "QuitButton", new Vector2(60f, 36.8f), 
                    new Vector2(15.0f, 5.5f), () => {_requestButton.interactable = false;_songRequestMenu.Present();_requestButton.interactable = true;}, "Song Requests");

                _requestButton.gameObject.GetComponentInChildren<TextMeshProUGUI>().enableWordWrapping = false;
                _requestButton.SetButtonTextSize(2.0f);
                UpdateRequestButton();
                BeatSaberUI.AddHintText(_requestButton.transform as RectTransform, $"{(!Config.Instance.SongRequestBot ? "To enable the song request bot, look in the Enhanced Twitch Chat settings menu." : "Manage the current request queue")}");
                Plugin.Log("Created request button!");
            }

            if (_songRequestListViewController == null)
                _songRequestListViewController = BeatSaberUI.CreateViewController<RequestBotListViewController>();

            if (_songRequestMenu == null)
            {
                _songRequestMenu = BeatSaberUI.CreateCustomMenu<CustomMenu>("Song Request Queue");
                _songRequestMenu.SetMainViewController(_songRequestListViewController, true);
            }

            SongListUtils.Initialize();

            if (Instance) return;
            new GameObject("EnhancedTwitchChatRequestBot").AddComponent<RequestBot>();
        }
        
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Instance = this;
            _songBlacklist = Config.Instance.Blacklist;

            if (Config.Instance.PersistentRequestQueue)
            {
                _persistentRequestQueue = Config.Instance.RequestQueue;
                if (_persistentRequestQueue.Count > 0)
                    LoadPersistentRequestQueue();
            }
            InitializeCommands();
        }
        
        private void LoadPersistentRequestQueue()
        {
            foreach(string request in _persistentRequestQueue)
            {
                string[] parts = request.Split('/');
                if (parts.Length > 1)
                {
                    RequestInfo info = new RequestInfo(new TwitchUser(parts[0]), parts[1], parts.Length > 2 ? DateTime.FromFileTime(long.Parse(parts[2])) : DateTime.UtcNow, true);
                    info.isPersistent = true;
                    Plugin.Log($"Checking request from {parts[0]}, song id is {parts[1]}");
                    UnverifiedRequestQueue.Enqueue(info);
                }
            }
        }

        private void FixedUpdate()
        {
            if (UnverifiedRequestQueue.Count > 0)
            {
                if (!_checkingQueue && UnverifiedRequestQueue.TryDequeue(out var requestInfo))
                {
                    StartCoroutine(CheckRequest(requestInfo));
                }
            }
            
            if (_botMessageQueue.Count > 0)
                SendChatMessage(_botMessageQueue.Dequeue());
        }

        private void SendChatMessage(string message)
        {
            try
            {
                Plugin.Log($"Sending message: \"{message}\"");
                TwitchWebSocketClient.SendMessage($"PRIVMSG #{Config.Instance.TwitchChannelName} :{message}");
                TwitchMessage tmpMessage = new TwitchMessage();
                tmpMessage.user = TwitchWebSocketClient.OurTwitchUser;
                MessageParser.Parse(new ChatMessage(message, tmpMessage));
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception was caught when trying to send bot message. {e.ToString()}");
            }
        }

        private void QueueChatMessage(string message)
        {
            _botMessageQueue.Enqueue(message);
        }

        private Dictionary<string, RequestUserTracker> _requestTracker = new Dictionary<string, RequestUserTracker>();
        private IEnumerator CheckRequest(RequestInfo requestInfo)
        {
            _checkingQueue = true;
            bool isPersistent = requestInfo.isPersistent;
            TwitchUser requestor = requestInfo.requestor;
            string request = requestInfo.request;
            if (requestInfo.isBeatSaverId)
            {
                foreach (SongRequest req in FinalRequestQueue.ToArray())
                {
                    var song = req.song;
                    if (song["id"].Value == request || song["version"].Value == request)
                    {
                        if(!isPersistent)
                            QueueChatMessage($"Request {song["songName"].Value} by {song["authorName"].Value} already exists in queue!");
                        _checkingQueue = false;
                        yield break;
                    }
                }
            }
            string requestUrl = requestInfo.isBeatSaverId ? "https://beatsaver.com/api/songs/detail" : "https://beatsaver.com/api/songs/search/song";
            using (var web = UnityWebRequest.Get($"{requestUrl}/{request}"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"Error {web.error} occured when trying to request song {request}!");
                    if (!isPersistent)
                        QueueChatMessage($"Invalid BeatSaver ID \"{request}\" specified.");
                    _checkingQueue = false;
                    yield break;
                }
                
                JSONNode result = JSON.Parse(web.downloadHandler.text);
                if (result["songs"].IsArray && result["total"].AsInt == 0)
                {
                    if (!isPersistent)
                        QueueChatMessage($"No results found for request \"{request}\"");
                    _checkingQueue = false;
                    yield break;
                }
                yield return null;

                JSONObject song = !result["songs"].IsArray ? result["song"].AsObject : result["songs"].AsArray[0].AsObject;
                if (FinalRequestQueue.Any(req => req.song["version"] == song["version"]))
                {
                    if (!isPersistent)
                        QueueChatMessage($"Request {song["songName"].Value} by {song["authorName"].Value} already exists in queue!");
                    _checkingQueue = false;
                    yield break;
                }
                else
                {
                    if (_songBlacklist.Contains(song["id"]))
                    {
                        if (!isPersistent)
                            QueueChatMessage($"{song["songName"].Value} by {song["authorName"].Value} is blacklisted!");
                        _checkingQueue = false;
                        yield break;
                    }

                    if(!isPersistent)
                        _requestTracker[requestor.id].numRequests++;
                    if (!isPersistent)
                    {
                        _persistentRequestQueue.Add($"{requestInfo.requestor.displayName}/{song["id"].Value}/{DateTime.UtcNow.ToFileTime()}");
                        Config.Instance.RequestQueue = _persistentRequestQueue;
                    }
                    FinalRequestQueue.Add(new SongRequest(song, requestor, requestInfo.requestTime, RequestStatus.Queued));
                    UpdateRequestButton();
                    if (!isPersistent)
                        QueueChatMessage($"Request {song["songName"].Value} by {song["authorName"].Value} added to queue.");

                    try
                    {
                        SongRequestQueued?.Invoke(song);
                    }
                    catch(Exception ex)
                    {
                        Plugin.Log(ex.ToString());
                    }
                }
            }
            _checkingQueue = false;
        }

        private static IEnumerator ProcessSongRequest(int index, bool fromHistory = false)
        {
            if (FinalRequestQueue.Count > 0)
            {
                SongRequest request = null;
                if(!fromHistory)
                {
                    SetRequestStatus(index, RequestStatus.Played);
                    request = index == -1 ? DequeueRequest(0) : DequeueRequest(index);
                }
                else
                {
                    request = SongRequestHistory.ElementAt(index);
                }

                if (request == null)
                {
                    Plugin.Log("Can't process a null request! Aborting!");
                    yield break;
                }
                else
                    Plugin.Log($"Processing song request {request.song["songName"].Value}");

                bool retried = false;
                string songIndex = request.song["version"].Value, songName = request.song["songName"].Value;
                string currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "CustomSongs", songIndex);
                string songHash = request.song["hashMd5"].Value.ToUpper();
            retry:
                CustomLevel[] levels = SongLoader.CustomLevels.Where(l => l.levelID.StartsWith(songHash)).ToArray();
                if (levels.Length == 0)
                {
                    Utilities.EmptyDirectory(".requestcache", false);

                    if (Directory.Exists(currentSongDirectory))
                    {
                        Utilities.EmptyDirectory(currentSongDirectory, true);
                        Plugin.Log($"Deleting {currentSongDirectory}");
                    }
                        
                    string localPath = Path.Combine(Environment.CurrentDirectory, ".requestcache", $"{songIndex}.zip");
                    yield return Utilities.DownloadFile(request.song["downloadUrl"].Value, localPath);
                    yield return Utilities.ExtractZip(localPath, currentSongDirectory);
                    yield return SongListUtils.RefreshSongs(false, false, true);

                    Utilities.EmptyDirectory(".requestcache", true);
                    levels = SongLoader.CustomLevels.Where(l => l.levelID.StartsWith(songHash)).ToArray();
                }
                else
                {
                    Plugin.Log($"Song {songName} already exists!");
                }

                if (levels.Length > 0)
                {
                    Plugin.Log($"Scrolling to level {levels[0].levelID}");
                    if (!SongListUtils.ScrollToLevel(levels[0].levelID) && !retried)
                    {
                        retried = true;
                        goto retry;
                    }
                }
                else
                {
                    Plugin.Log("Failed to find new level!");
                }
                _songRequestMenu.Dismiss();
            }
        }

        private void InitializeCommands()
        {
            foreach (string c in Config.Instance.RequestCommandAliases.Split(','))
            {
                Commands.Add(c, ProcessSongRequest);
                Plugin.Log($"Added command alias \"{c}\" for song requests.");
            }
        }

        private void ProcessSongRequest(TwitchUser requestor, string request)
        {
            if (!_requestTracker.ContainsKey(requestor.id))
                _requestTracker.Add(requestor.id, new RequestUserTracker());

            // Only rate limit users who aren't mods or the broadcaster
            if (!requestor.isMod && !requestor.isBroadcaster)
            {
                if (_requestTracker[requestor.id].resetTime <= DateTime.Now)
                {
                    _requestTracker[requestor.id].resetTime = DateTime.Now.AddMinutes(Config.Instance.RequestCooldownMinutes);
                    _requestTracker[requestor.id].numRequests = 0;
                }
                if (_requestTracker[requestor.id].numRequests >= Config.Instance.RequestLimit)
                {
                    var time = (_requestTracker[requestor.id].resetTime - DateTime.Now);
                    QueueChatMessage($"{requestor.displayName}, you can make another request in{(time.Minutes > 0 ? $" {time.Minutes} minute{(time.Minutes > 1 ? "s" : "")}" : "")} {time.Seconds} second{(time.Seconds > 1 ? "s" : "")}.");
                    return;
                }
            }

            RequestInfo newRequest = new RequestInfo(requestor, request, DateTime.UtcNow, _digitRegex.IsMatch(request) || _beatSaverRegex.IsMatch(request));
            if (!newRequest.isBeatSaverId && request.Length < 3)
                Instance.QueueChatMessage($"Request \"{request}\" is too short- Beat Saver searches must be at least 3 characters!");
            else if (!UnverifiedRequestQueue.Contains(newRequest))
                UnverifiedRequestQueue.Enqueue(newRequest);
        }
        
        private static void UpdateRequestButton()
        {
            if (FinalRequestQueue.Count == 0)
            {
                //_requestButton.interactable = false;
                _requestButton.gameObject.GetComponentInChildren<Image>().color = Color.red;
            }
            else
            {
                //_requestButton.interactable = true;
                _requestButton.gameObject.GetComponentInChildren<Image>().color = Color.green;
            }
        }

        public static void DequeueRequest(SongRequest request)
        {
            SongRequestHistory.Insert(0, request);
            FinalRequestQueue.Remove(request);
            
            var matches = _persistentRequestQueue.Where(r => r != null && r.StartsWith($"{request.requestor.displayName}/{request.song["id"]}"));
            if (matches.Count() > 0)
            {
                _persistentRequestQueue.Remove(matches.First());
                Config.Instance.RequestQueue = _persistentRequestQueue;
            }
            UpdateRequestButton();
            try
            {
                SongRequestDequeued?.Invoke(request.song);
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
        }

        public static SongRequest DequeueRequest(int index)
        {
            SongRequest request = FinalRequestQueue.ElementAt(index);
            if (request != null)
                DequeueRequest(request);
            return request;
        }

        public static void SetRequestStatus(int index, RequestStatus status, bool fromHistory = false)
        {
            if (!fromHistory)
                FinalRequestQueue[index].status = status;
            else
                SongRequestHistory[index].status = status;
        }
        
        public static void Blacklist(int index, bool fromHistory)
        {
            // Add the song to the blacklist
            SongRequest request = fromHistory ? SongRequestHistory.ElementAt(index) : FinalRequestQueue.ElementAt(index);
            _songBlacklist.Add(request.song["id"].Value);
            Config.Instance.Blacklist = _songBlacklist;
            Instance.QueueChatMessage($"{request.song["songName"].Value} by {request.song["authorName"].Value} is now blacklisted!");

            if (!fromHistory)
            {
                // Then skip the request
                Skip(index, RequestStatus.Blacklisted);
            }
            else
                SetRequestStatus(index, RequestStatus.Blacklisted, fromHistory);
        }

        public static void Skip(int index, RequestStatus status = RequestStatus.Skipped)
        {
            // Set the final status of the request
            SetRequestStatus(index, status);

            // Then dequeue it
            DequeueRequest(index);
        }

        public static void Process(int index, bool fromHistory)
        {
            Instance?.StartCoroutine(ProcessSongRequest(index, fromHistory));
        }

        public static void Next()
        {
            Instance?.StartCoroutine(ProcessSongRequest(-1));
        }

        public static void Parse(TwitchUser user, string request)
        {
            if (!Instance) return;
            if (!request.StartsWith("!")) return;
            string[] parts = request.Split(new char[] { ' ' }, 2);
            if (parts.Length <= 1) return;
            
            string command = parts[0].Substring(1)?.ToLower();
            if (Commands.ContainsKey(command))
                Commands[command]?.Invoke(user, parts[1]);
        }
    }
}
