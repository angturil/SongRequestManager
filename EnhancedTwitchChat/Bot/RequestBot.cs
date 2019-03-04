//#define PRIVATE 

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
    public partial class RequestBot : MonoBehaviour
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

        public string EnhancedTwitchchatFiles = "\\requestqueue\\";

        private static readonly Regex _digitRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _beatSaverRegex = new Regex("^[0-9]+-[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _alphaNumericRegex = new Regex("^[0-9A-Za-z]+$", RegexOptions.Compiled); 

        public static RequestBot Instance;
        public static ConcurrentQueue<RequestInfo> UnverifiedRequestQueue = new ConcurrentQueue<RequestInfo>();
        public static List<SongRequest> FinalRequestQueue = new List<SongRequest>();
        public static List<SongRequest> SongRequestHistory = new List<SongRequest>();
        private static Button _requestButton;
        private static bool _checkingQueue = false;
        private static bool _refreshQueue = false;

        private static FlowCoordinator _levelSelectionFlowCoordinator;
        private static DismissableNavigationController _levelSelectionNavigationController;
        private static Queue<string> _botMessageQueue = new Queue<string>();
        private static Dictionary<string, Action<TwitchUser, string>> Commands = new Dictionary<string, Action<TwitchUser, string>>();

        static public bool QueueOpen = false;
        bool mapperwhiteliston = false;
        bool mapperblackliston = false;

        private static System.Random generator = new System.Random();

        public static List<JSONObject> played = new List<JSONObject>(); // Played list

        private static List<string> mapperwhitelist = new List<string>(); // Lists because we need to interate them per song
        private static List<string> mapperblacklist = new List<string>();

        private static HashSet<string> _songBlacklist = new HashSet<string>();
        private static HashSet<string> duplicatelist = new HashSet<string>();
        private static Dictionary<string, string> songremap = new Dictionary<string, string>();
        private static Dictionary<string, string> deck = new Dictionary<string, string>(); // deck name/content

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
                    new Vector2(15.0f, 5.5f), () => { _requestButton.interactable = false; _songRequestMenu.Present(); _requestButton.interactable = true; }, "Song Requests");

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

            Directory.CreateDirectory($"{Environment.CurrentDirectory}\\requestqueue");
            WriteQueueSummaryToFile();
            WriteQueueStatusToFile(QueueOpen ? "Queue is open" : "Queue is closed");


            if (Instance) return;
            new GameObject("EnhancedTwitchChatRequestBot").AddComponent<RequestBot>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Instance = this;

            string filesToDelete = Path.Combine(Environment.CurrentDirectory, "FilesToDelete");
            if (Directory.Exists(filesToDelete))
                Utilities.EmptyDirectory(filesToDelete);

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
            foreach (string request in _persistentRequestQueue)
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

            if(_refreshQueue)
            {
                if(RequestBotListViewController.Instance.isActivated)
                    RequestBotListViewController.Instance.UpdateRequestUI(true);
                _refreshQueue = false;
            }
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

        public static Dictionary<string, RequestUserTracker> _requestTracker = new Dictionary<string, RequestUserTracker>();

        public void QueueChatMessage(string message)
        {
            _botMessageQueue.Enqueue(message);
        }

        private string GetStarRating(ref JSONObject song)
        {
            string stars = "******";
            float rating = song["rating"].AsFloat;
            if (rating < 0 || rating > 100) rating = 0;
            string starrating=stars.Substring(0, (int)(rating / 17)); // 17 is used to produce a 5 star rating from 80ish to 100.
            return starrating;
        }

        private IEnumerator CheckRequest(RequestInfo requestInfo)
        {
            _checkingQueue = true;

            TwitchUser requestor = requestInfo.requestor;

            bool isPersistent = requestInfo.isPersistent;
           
            string request = requestInfo.request;

            // Special code for numeric searches
            if (requestInfo.isBeatSaverId)
            {
                // Remap song id if entry present. This is one time, and not correct as a result. No recursion right now, could be confusing to the end user.
                string[] requestparts = request.Split(new char[] { '-' }, 2);

                if (!isPersistent && requestparts.Length > 0 && songremap.ContainsKey(requestparts[0]))
                {
                    request = songremap[requestparts[0]];
                    QueueChatMessage($"Remapping request {requestInfo.request} to {request}");
                }

                string requestcheckmessage = IsRequestInQueue(request);               // Check if requested ID is in Queue  
                if (requestcheckmessage!="")
                    {
                    if (!isPersistent) QueueChatMessage(requestcheckmessage);
                    _checkingQueue = false;
                    yield break;
                    }
            }

            // Get song query results from beatsaver.com

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

                List<JSONObject> songs = new List<JSONObject>();                 // Load resulting songs into a list 

                string errormessage = "";

                if (result["songs"].IsArray)
                {
                    foreach (JSONObject currentSong in result["songs"].AsArray)
                    {
                        errormessage = SongSearchFilter(currentSong, false);
                        if (errormessage== "") songs.Add(currentSong);
                    }
                }
                else
                {
                    songs.Add(result["song"].AsObject);
                }


                // Filter out too many or too few results
                if (songs.Count == 0)
                    {
                    if (errormessage=="") errormessage = $"No results found for request \"{request}\"";
                    }
                else if (songs.Count >= 4)
                    errormessage = $"Request for '{request}' produces {songs.Count} results, narrow your search by adding a mapper name, or use https://beatsaver.com to look it up.";
                else if (songs.Count > 1 && songs.Count < 4)
                {
                    string songlist = $"@{requestor.displayName}, please choose: ";
                    foreach (var eachsong in songs) songlist += $"{eachsong["songName"].Value}-{eachsong["songSubName"].Value}-{eachsong["authorName"].Value} ({eachsong["version"].Value}), ";
                    errormessage = songlist.Substring(0, songlist.Length - 2); // Remove trailing ,'s
                }
                else
                {
                    if (isNotModerator(requestor) || !requestInfo.isBeatSaverId) errormessage = SongSearchFilter(songs[0], false);
                }

                // Display reason why chosen song was rejected, if filter is triggered. Do not add filtered songs
                if (errormessage != "")
                    {
                    if (!isPersistent) QueueChatMessage(errormessage);
                    _checkingQueue = false;
                    yield break;
                    }

                var song = songs[0];

                if (!isPersistent)
                    {
                    _requestTracker[requestor.id].numRequests++;
                    duplicatelist.Add(song["id"].Value);
                    _persistentRequestQueue.Add($"{requestInfo.requestor.displayName}/{song["id"].Value}/{DateTime.UtcNow.ToFileTime()}");
                    Config.Instance.RequestQueue = _persistentRequestQueue;
                     }

                    FinalRequestQueue.Add(new SongRequest(song, requestor, requestInfo.requestTime, RequestStatus.Queued));

                if (!isPersistent)
                    {
                    Writedeck(requestor, "savedqueue"); // Might not be needed.. logic around saving and loading deck state needs to be reworked
                    QueueChatMessage($"Request {song["songName"].Value} by {song["authorName"].Value} {GetStarRating(ref song)} ({song["version"].Value}) added to queue.");
                    }

                UpdateRequestButton();

                _refreshQueue = true;
                }
            _checkingQueue = false;
        }

        private static IEnumerator ProcessSongRequest(int index, bool fromHistory = false)
        {
            if ((FinalRequestQueue.Count > 0 && !fromHistory) || (SongRequestHistory.Count > 0 && fromHistory))
            {
                SongRequest request = null;
                if (!fromHistory)
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


                var song = request.song;

                // BUG: Songs status chat messages need to be configurable.
                //Instance.QueueChatMessage($"{song["songName"].Value} by {song["authorName"].Value} ({song["version"].Value}) is next."); // UI Setting needed to toggle this on/off
                //Instance.QueueChatMessage($"{song["songName"].Value} by {song["authorName"].Value} https://beatsaver.com/browse/detail/{song["version"].Value} is next."); // UI Setting needed to toggle this on/off
                Instance.QueueChatMessage($"{song["songName"].Value} by {song["authorName"].Value} https://bsaber.com/songs/{song["id"].Value} is next."); // UI Setting needed to toggle this on/off

                _songRequestMenu.Dismiss();
            }
        }
        

        private static void UpdateRequestButton()
        {

            try
            {
                RequestBot.WriteQueueSummaryToFile(); // Write out queue status to file, do it first

                if (FinalRequestQueue.Count == 0)
                {
                    _requestButton.gameObject.GetComponentInChildren<Image>().color = Color.red;
                }
                else
                {
                    _requestButton.gameObject.GetComponentInChildren<Image>().color = Color.green;
                }

            }
            catch
             {
             
            }

        }


        public static void DequeueRequest(SongRequest request)
        {
            SongRequestHistory.Insert(0, request);
            FinalRequestQueue.Remove(request);

            // Decrement the requestors request count, since their request is now out of the queue
            if (_requestTracker.ContainsKey(request.requestor.id)) _requestTracker[request.requestor.id].numRequests--;

            var matches = _persistentRequestQueue.Where(r => r != null && r.StartsWith($"{request.requestor.displayName}/{request.song["id"]}"));
            if (matches.Count() > 0)
            {
                _persistentRequestQueue.Remove(matches.First());
                Config.Instance.RequestQueue = _persistentRequestQueue;
            }
            UpdateRequestButton();
            _refreshQueue = true;
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

        public static void Blacklist(int index, bool fromHistory, bool skip)
        {
            // Add the song to the blacklist
            SongRequest request = fromHistory ? SongRequestHistory.ElementAt(index) : FinalRequestQueue.ElementAt(index);
            _songBlacklist.Add(request.song["id"].Value);
            Config.Instance.Blacklist = _songBlacklist;
            Instance.QueueChatMessage($"{request.song["songName"].Value} by {request.song["authorName"].Value} is now blacklisted!");

            if (!fromHistory)
            {
                if(skip)
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


        private void InitializeCommands()
        {
            foreach (string c in Config.Instance.RequestCommandAliases.Split(',').Distinct())
            {
                Commands.Add(c, ProcessSongRequest);
                Plugin.Log($"Added command alias \"{c}\" for song requests.");
            }

            ReadRemapList();

            Commands.Add("queue", ListQueue);
            Commands.Add("unblock", Unban);
            Commands.Add("block", Ban);
            Commands.Add("remove", DequeueSong);
            Commands.Add("clearqueue", Clearqueue);
            Commands.Add("mtt", MoveRequestToTop);
            Commands.Add("remap", Remap);
            Commands.Add("unmap", Unmap);
            Commands.Add("lookup", lookup);
            Commands.Add("find", lookup);
            Commands.Add("last", MoveRequestToBottom);
            Commands.Add("demote", MoveRequestToBottom);
            Commands.Add("later", MoveRequestToBottom);
            Commands.Add("wrongsong", WrongSong);
            Commands.Add("wrong", WrongSong);
            Commands.Add("oops", WrongSong);
            Commands.Add("blist", ShowBanList);
            Commands.Add("open", OpenQueue);
            Commands.Add("close", CloseQueue);
            Commands.Add("restore", restoredeck);
            Commands.Add("commandlist", showCommandlist);
            Commands.Add("played", ShowSongsplayed);
            Commands.Add("readdeck", Readdeck);
            Commands.Add("writedeck", Writedeck);

#if PRIVATE

            Commands.Add("goodmappers",mapperWhitelist);
            Commands.Add("mapperwhitelist",mapperWhitelist);                  
            Commands.Add("addnew",addNewSongs);
            Commands.Add("addlatest",addNewSongs);          
            Commands.Add("deck",createdeck);
            Commands.Add("unloaddeck",unloaddeck);
            Commands.Add("requested", ListPlayedList);       
            Commands.Add("mapper", addsongsbymapper);
            Commands.Add("addsongs",addSongs);
            Commands.Add("loaddecks",loaddecks);
            Commands.Add("decklist",decklist);
            Commands.Add("badmappers",mapperBlacklist);
            Commands.Add("mapperblacklist",mapperBlacklist);

            mapperWhitelist(TwitchWebSocketClient.OurTwitchUser,"mapper");
            loaddecks (TwitchWebSocketClient.OurTwitchUser,"");

#endif
        }


        private void lookup(TwitchUser requestor, string request)
        {
            if (isNotModerator(requestor) && !requestor.isSub)
            {
                QueueChatMessage($"lookup command is limited to Subscribers and moderators.");
                return;
            }

            StartCoroutine(LookupSongs(requestor, request));

        }


        private string GetBeatSaverId(string request)
        {
            if (_digitRegex.IsMatch(request)) return request;
            if (_beatSaverRegex.IsMatch(request))
            {
                string[] requestparts = request.Split(new char[] { '-' }, 2);
                return requestparts[0];
            }
            return "";
        }
        
        private void ProcessSongRequest(TwitchUser requestor, string request)
        {
            try
            {
                if (QueueOpen == false && isNotModerator(requestor))
                {
                    QueueChatMessage($"Queue is currently closed.");
                    return;
                }

                if (request == "")
                {
                    // Would be nice if it was configurable
                    QueueChatMessage($"usage: bsr <song id> or <part of song name and mapper if known>");
                    return;
                }

                if (!_requestTracker.ContainsKey(requestor.id))
                    _requestTracker.Add(requestor.id, new RequestUserTracker());
                
                int limit = Config.Instance.RequestLimit;
                if (requestor.isSub) limit = Math.Max(limit, Config.Instance.SubRequestLimit);
                if (requestor.isMod) limit = Math.Max(limit, Config.Instance.ModRequestLimit);
                if (requestor.isVip) limit+=Config.Instance.VipBonus; // Current idea is to give VIP's a bonus over their base subscription class, you can set this to 0 if you like

                /*
                // Currently using simultaneous request limits, will be introduced later / or activated if time mode is on.
                // Only rate limit users who aren't mods or the broadcaster - 
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
                */

                // Only rate limit users who aren't mods or the broadcaster
                if (!requestor.isBroadcaster)
                {
                    if (_requestTracker[requestor.id].numRequests >= limit)
                    {
                        QueueChatMessage($"You already have {_requestTracker[requestor.id].numRequests} on the queue. You can add another once one is played. Subscribers are limited to {Config.Instance.SubRequestLimit}.");
                        return;
                    }
                }
                
                RequestInfo newRequest = new RequestInfo(requestor, request, DateTime.UtcNow, _digitRegex.IsMatch(request) || _beatSaverRegex.IsMatch(request));
                if (!newRequest.isBeatSaverId && request.Length < 3)
                    QueueChatMessage($"Request \"{request}\" is too short- Beat Saver searches must be at least 3 characters!");
                else if (!UnverifiedRequestQueue.Contains(newRequest))
                    UnverifiedRequestQueue.Enqueue(newRequest);

            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());

            }
        }
        
        public static void Process(int index)
        {
            Instance?.StartCoroutine(ProcessSongRequest(index));
        }
        
        public static void Parse(TwitchUser user, string request)
        {
            if (!Instance) return;
            if (!request.StartsWith("!")) return;

            string[] parts = request.Split(new char[] { ' ' }, 2);

            if (parts.Length <= 0) return;

            string command = parts[0].Substring(1)?.ToLower();
            if (Commands.ContainsKey(command))
            {
                string param = parts.Length > 1 ? parts[1] : "";
                if (deck.ContainsKey(command))
                {
                    param = command;
                    if (parts.Length > 1) param += " " + parts[1];
                }
                Commands[command]?.Invoke(user, param);
            }
        }

       

    }
}



