using AsyncTwitch;
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

namespace EnhancedTwitchChat.Chat
{
    public class RequestBot : MonoBehaviour
    {
        public class RequestInfo
        {
            public ChatUser requestor;
            public string request;
            public bool isBeatSaverId;
        }

        private static readonly Regex _digitRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _beatSaverRegex = new Regex("^[0-9]+-[0-9]+$", RegexOptions.Compiled);

        private static RequestBot _instance;
        public static ConcurrentQueue<RequestInfo> UnverifiedRequestQueue = new ConcurrentQueue<RequestInfo>();
        public static ConcurrentQueue<JSONObject> FinalRequestQueue = new ConcurrentQueue<JSONObject>();
        private bool _checkingQueue = false;
        private static Button _requestButton;

        private static FlowCoordinator _levelSelectionFlowCoordinator;
        private static DismissableNavigationController _levelSelectionNavigationController;
        private static Queue<string> _botMessageQueue = new Queue<string>();
        private static Dictionary<string, Action<ChatUser,string>> Commands = new Dictionary<string, Action<ChatUser,string>>();

        public static void OnLoad()
        {
            _levelSelectionFlowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
            if (_levelSelectionFlowCoordinator)
                _levelSelectionNavigationController = _levelSelectionFlowCoordinator.GetPrivateField<DismissableNavigationController>("_navigationController");

            if (_levelSelectionNavigationController)
            {
                _requestButton = BeatSaberUI.CreateUIButton(_levelSelectionNavigationController.transform as RectTransform, "QuitButton", new Vector2(60f, 36.8f), new Vector2(15.0f, 5.5f), () => RequestBot.NextRequest(), "Next Request");
                _requestButton.gameObject.GetComponentInChildren<TextMeshProUGUI>().enableWordWrapping = false;
                _requestButton.SetButtonTextSize(2.0f);
                _requestButton.interactable = false;
                _requestButton.gameObject.GetComponentInChildren<Image>().color = Color.red;
                BeatSaberUI.AddHintText(_requestButton.transform as RectTransform, $"{(!Config.Instance.SongRequestBot ? "To enable the song request bot, look in the Enhanced Twitch Chat settings menu." : "Moves onto the next song request in the queue\r\n\r\n<size=60%>Use <b>!request <beatsaver-id></b> or <b>!request <song name></b> to request songs!</size>")}");
                Plugin.Log("Created request button!");
            }

            SongListUtils.Initialize();

            if (_instance) return;
            new GameObject("EnhancedTwitchChatRequestBot").AddComponent<RequestBot>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _instance = this;

            InitializeCommands();
        }

        private void FixedUpdate()
        {
            if (UnverifiedRequestQueue.Count > 0)
            {
                if (!_checkingQueue && UnverifiedRequestQueue.TryDequeue(out var requestInfo))
                {
                    StartCoroutine(CheckRequest(requestInfo));
                    _checkingQueue = true;
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
                TwitchConnection.Instance.SendRawMessage($"PRIVMSG #{TwitchIRCClient.CurrentChannel} :{message}");
                TwitchMessage tmpMessage = new TwitchMessage();
                tmpMessage.Author = TwitchIRCClient.OurChatUser;
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
        
        class RequestUserTracker
        {
            public int numRequests = 0;
            public DateTime resetTime = DateTime.Now;
        }

        private Dictionary<string, RequestUserTracker> _requestTracker = new Dictionary<string, RequestUserTracker>();
        private IEnumerator CheckRequest(RequestInfo requestInfo)
        {
            ChatUser requestor = requestInfo.requestor;
            string request = requestInfo.request;
            if (requestInfo.isBeatSaverId)
            {
                foreach (JSONObject r in FinalRequestQueue.ToArray())
                {
                    if (((string)r["version"]).StartsWith(request))
                    {
                        QueueChatMessage($"Request {r["songName"]} by {r["authorName"]} already exists in queue!");
                        _checkingQueue = false;
                        yield break;
                    }
                }
            }
            Plugin.Log("8");
            string requestUrl = requestInfo.isBeatSaverId ? "https://beatsaver.com/api/songs/detail" : "https://beatsaver.com/api/songs/search/song";
            using (var web = UnityWebRequest.Get($"{requestUrl}/{request}"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"Error {web.error} occured when trying to request song {request}!");
                    QueueChatMessage($"Invalid BeatSaver ID \"{request}\" specified.");
                    _checkingQueue = false;
                    yield break;
                }

                JSONNode result = JSON.Parse(web.downloadHandler.text);
                if (result["songs"].IsArray && result["total"].AsInt == 0)
                {
                    QueueChatMessage($"No results found for request \"{request}\"");
                    _checkingQueue = false;
                    yield break;
                }
                yield return null;
                
                JSONObject song = !result["songs"].IsArray ? result["song"].AsObject : result["songs"].AsArray[0].AsObject;
                if (FinalRequestQueue.Any(j => j["version"] == song["version"]))
                {
                    QueueChatMessage($"Request {song["songName"]} by {song["authorName"]} already exists in queue!");
                    _checkingQueue = false;
                    yield break;
                }
                else
                {
                    _requestTracker[requestor.UserID].numRequests++;
                    FinalRequestQueue.Enqueue(song);
                    _requestButton.interactable = true;
                    _requestButton.gameObject.GetComponentInChildren<Image>().color = Color.green;
                    QueueChatMessage($"Request {song["songName"]} by {song["authorName"]} added to queue.");
                }
            }
            _checkingQueue = false;
        }

        private static IEnumerator ProcessNextRequest()
        {
            if (FinalRequestQueue.Count > 0)
            {
                if (FinalRequestQueue.TryDequeue(out var song))
                {
                    if (FinalRequestQueue.Count == 0)
                    {
                        _requestButton.interactable = false;
                        _requestButton.gameObject.GetComponentInChildren<Image>().color = Color.red;
                    }

                    string songIndex = song["version"], songName = song["songName"];
                    string currentSongDirectory = $"{Environment.CurrentDirectory}\\CustomSongs\\{songIndex}";
                    string songHash = ((string)song["hashMd5"]).ToUpper();
                    bool retried = false;

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

                        string localPath = $"{Environment.CurrentDirectory}\\.requestcache\\{songIndex}.zip";
                        yield return Utilities.DownloadFile(song["downloadUrl"], localPath);
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
                            var tempLevels = SongLoader.CustomLevels.Where(l => l.levelID.StartsWith(songHash)).ToArray();
                            foreach (CustomLevel l in tempLevels)
                                SongLoader.CustomLevels.Remove(l);

                            retried = true;
                            goto retry;
                        }
                    }
                    else
                    {
                        Plugin.Log("Failed to find new level!");
                    }
                }
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

        private void ProcessSongRequest(ChatUser requestor, string request)
        {
            if (!_requestTracker.ContainsKey(requestor.UserID))
                _requestTracker.Add(requestor.UserID, new RequestUserTracker());

            // Only rate limit users who aren't mods or the broadcaster
            if (!requestor.IsMod && !requestor.IsBroadcaster)
            {
                if (_requestTracker[requestor.UserID].resetTime <= DateTime.Now)
                {
                    _requestTracker[requestor.UserID].resetTime = DateTime.Now.AddMinutes(Config.Instance.RequestCooldownMinutes);
                    _requestTracker[requestor.UserID].numRequests = 0;
                }
                if (_requestTracker[requestor.UserID].numRequests >= Config.Instance.RequestLimit)
                {
                    var time = (_requestTracker[requestor.UserID].resetTime - DateTime.Now);
                    QueueChatMessage($"{requestor.DisplayName}, you can make another request in{(time.Minutes > 0 ? $" {time.Minutes} minute{(time.Minutes > 1 ? "s" : "")}" : "")} {time.Seconds} second{(time.Seconds > 1 ? "s" : "")}.");
                    return;
                }
            }

            RequestInfo newRequest = new RequestInfo()
            {
                requestor = requestor,
                request = request,
                isBeatSaverId = _digitRegex.IsMatch(request) || _beatSaverRegex.IsMatch(request)
            };

            if (!newRequest.isBeatSaverId && request.Length < 3)
                _instance.QueueChatMessage($"Request \"{request}\" is too short- Beat Saver searches must be at least 3 characters!");
            else if (!UnverifiedRequestQueue.Contains(newRequest))
                UnverifiedRequestQueue.Enqueue(newRequest);
        }

        public static void Parse(ChatUser user,  string request)
        {
            if (!_instance) return;
            if (!request.StartsWith("!")) return;

            string[] parts = request.Split(new char[] { ' ' }, 2);
            if (parts.Length <= 1) return;

            string command = parts[0].Substring(1)?.ToLower();
            if(Commands.ContainsKey(command))
                Commands[command]?.Invoke(user, parts[1]);
        }

        public static void NextRequest()
        {
            _instance?.StartCoroutine(ProcessNextRequest());
        }
    }
}
