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

        public class RequestUserTracker
        {
            public int numRequests = 0;
            public DateTime resetTime = DateTime.Now;
        }
        
        private static readonly Regex _digitRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _beatSaverRegex = new Regex("^[0-9]+-[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _alphanumeric=new Regex("^[0-9A-Za-z]+$", RegexOptions.Compiled);      // To be used to filter filenames

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

        static public bool QueueOpen = false;
        bool mapperwhiteliston = false;
        bool mapperblackliston = false;
        private static bool whiteliston = false;

        private static System.Random generator = new System.Random();

        public static List<JSONObject> played = new List<JSONObject>(); // Played list

        private static List<string> mapperwhitelist = new List<string>(); // Lists because we need to interate them per song
        private static List<string> mapperblacklist = new List<string>();

        private static HashSet<string> _songBlacklist = new HashSet<string>();
        private static HashSet<string> whitelist = new HashSet<string>();
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

            Directory.CreateDirectory($"{Environment.CurrentDirectory}\\requestqueue");
            writequeuesummarytofile();
            Writequeuestatustofile(QueueOpen ? "Queue is open" : "Queue is closed");


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

        public static Dictionary<string, RequestUserTracker> _requestTracker = new Dictionary<string, RequestUserTracker>();

        public void QueueChatMessage(string message)
        {
            _botMessageQueue.Enqueue(message);
        }


        private IEnumerator CheckRequest(RequestInfo requestInfo)
        {
            _checkingQueue = true;
            TwitchUser requestor = requestInfo.requestor;

            bool isPersistent = requestInfo.isPersistent;


            bool mod = requestor.isBroadcaster || requestor.isMod;

            string request = requestInfo.request;

            if (requestInfo.isBeatSaverId)
            {
                string[] requestparts = request.Split(new char[] { '-' }, 2);
                if (!isPersistent && requestparts.Length > 0 && songremap.ContainsKey(requestparts[0]) && !requestor.isBroadcaster)
                {
                    request = songremap[requestparts[0]];
                    QueueChatMessage($"Remapping request {requestInfo.request} to {request}");
                }


                foreach (SongRequest req in FinalRequestQueue.ToArray())
                {
                    var song = req.song;

                    string[] parts = ((string)song["version"]).Split(new char[] { '-' }, 2);

                    if (parts[0] == request || (string)song["version"] == request)
                    {
                        if (!isPersistent) QueueChatMessage($"Request {song["songName"].Value} by {song["authorName"].Value} ({song["version"].Value}) already exists in queue!");
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


                // JSONObject song = !result["songs"].IsArray ? result["song"].AsObject : result["songs"].AsArray[0].AsObject;

                // Load resulting songs into a list 

                List<JSONObject> songs = new List<JSONObject>();

                if (result["songs"].IsArray)
                {
                    for (int i = 0; i < result["songs"].AsArray.Count; i++)
                    {
                        JSONObject currentsong = result["songs"].AsArray[i].AsObject;

                        string songid = currentsong["id"].Value;

                        if (!requestor.isBroadcaster && whiteliston && !whitelist.Contains(songid)) continue;

                        if (!requestor.isBroadcaster && (mapperblackliston || mapperwhiteliston) && mapperfiltered(ref currentsong)) continue;

                        if (!requestor.isBroadcaster)
                        {
                            if (_songBlacklist.Contains(songid)) continue;
                            if (songremap.ContainsKey(songid)) continue;
                        }

                        songs.Add(currentsong);
                    }
                }
                else
                {

                    string songid = (result["song"].AsObject)["id"].Value;
                    //if (!_songBlacklist.Contains(songid))
                    {
                        songs.Add(result["song"].AsObject);
                    }
                }

                if (songs.Count == 0)
                {
                    QueueChatMessage($"No results found for request \"{request}\"");
                    _checkingQueue = false;
                    yield break;
                }


                JSONObject song = songs[0];


                if (songs.Count > 1 && songs.Count < 4)
                {
                    string songlist = "found: ";
                    for (int i = 0; i < songs.Count; i++)
                    {
                        song = songs[i];
                        if (i > 0) songlist += ", ";
                        songlist += $"{song["songName"].Value}-{song["songSubName"].Value}-{song["authorName"].Value} ({song["version"].Value})";
                    }

                    QueueChatMessage(songlist);

                    _checkingQueue = false;
                    yield break;
                }

                if (songs.Count >= 4)
                {

                    QueueChatMessage($"Request for '{request}' produces {songs.Count} results, narrow your search by adding a mapper name, or use https://beatsaver.com to look it up.");
                    _checkingQueue = false;
                    yield break;
                }

                if (FinalRequestQueue.Any(req => req.song["version"] == song["version"]))
                {
                    QueueChatMessage($"Request {song["songName"].Value} by {song["authorName"].Value} already exists in queue!");
                    _checkingQueue = false;
                    yield break;
                }
                else
                {
                    if (_songBlacklist.Contains(song["id"]))
                    {
                        QueueChatMessage($"{song["songName"].Value} by {song["authorName"].Value} ({song["version"].Value}) is blacklisted!");
                        _checkingQueue = false;
                        yield break;
                    }


                    if (!requestor.isBroadcaster && mapperwhiteliston && mapperfiltered(ref song))
                    {
                        QueueChatMessage($"{song["songName"].Value} by {song["authorName"].Value} does not have a permitted mapper!");
                        _checkingQueue = false;
                        yield break;

                    }


                    if (duplicatelist.Contains(song["id"]) && (!requestor.isBroadcaster))
                    {
                        QueueChatMessage($"{song["songName"].Value} by {song["authorName"].Value} has already been requested this session!");
                        _checkingQueue = false;
                        yield break;
                    }


                    if (!requestor.isBroadcaster && whiteliston && !whitelist.Contains(song["id"].Value))
                    {
                        QueueChatMessage($"{song["songName"].Value} by {song["authorName"].Value} is not permitted this session!");
                        _checkingQueue = false;
                        yield break;
                    }


                    Writedeck(requestor, "savedqueue");


                    if (!isPersistent)
                        _requestTracker[requestor.id].numRequests++;
                    if (!isPersistent)
                    {
                        duplicatelist.Add(song["id"].Value);
                        _persistentRequestQueue.Add($"{requestInfo.requestor.displayName}/{song["id"].Value}/{DateTime.UtcNow.ToFileTime()}");
                        Config.Instance.RequestQueue = _persistentRequestQueue;
                    }
                    FinalRequestQueue.Add(new SongRequest(song, requestor, requestInfo.requestTime, RequestStatus.Queued));
                    UpdateRequestButton();
                    if (!isPersistent)
                        QueueChatMessage($"Request {song["songName"].Value} by {song["authorName"].Value} ({song["version"].Value}) added to queue.");


                    try
                    {
                        SongRequestQueued?.Invoke(song);
                    }
                    catch (Exception ex)
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

                var song = request.song;
                Instance.QueueChatMessage($"{song["songName"].Value} by {song["authorName"].Value} ({song["version"].Value}) is next."); // UI Setting needed to toggle this on/off
                _songRequestMenu.Dismiss();
            }
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

            RequestBot.writequeuesummarytofile(); // Write out queue status to file
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


        private void InitializeCommands()
        {
            foreach (string c in Config.Instance.RequestCommandAliases.Split(','))
            {
                Commands.Add(c, ProcessSongRequest);
                Plugin.Log($"Added command alias \"{c}\" for song requests.");
            }

            ReadRemapList();

            Commands.Add("queue", ListQueue);
            Commands.Add("unblock", UnBan);
            Commands.Add("block", Block);
            Commands.Add("remove", Unqueuesong);
            Commands.Add("clearqueue", Clearqueue);
            Commands.Add("mtt", mtt);
            Commands.Add("remap", Remap);
            Commands.Add("unmap", Unmap);
            Commands.Add("lookup", lookup);
            Commands.Add("find", lookup);
            Commands.Add("last", last);
            Commands.Add("demote", last);
            Commands.Add("later", last);
            Commands.Add("wrongsong", WrongSong);
            Commands.Add("wrong", WrongSong);
            Commands.Add("oops", WrongSong);
            Commands.Add("blist", showBanlist);
            Commands.Add("open", openQueue);
            Commands.Add("close", closeQueue);
            Commands.Add("restore", restoredeck);
            Commands.Add("commandlist", showCommandlist);
            Commands.Add("played", showSongsplayed);

#if PRIVATE

            Commands.Add("readdeck", Readdeck);
            Commands.Add("writedeck", Writedeck);
            Commands.Add("goodmappers",mapperWhitelist);
            Commands.Add("mapperwhitelist",mapperWhitelist);                  
            Commands.Add("addnew",addNewSongs);
            Commands.Add("addlatest",addNewSongs);          
            Commands.Add("deck",createdeck);
            Commands.Add("unloaddeck",unloaddeck);
            Commands.Add("requested",ListPlayedlist);       
            Commands.Add("mapper", addsongsbymapper);
            Commands.Add("addsongs",addSongs);
            Commands.Add("loaddecks",loaddecks);
            Commands.Add("decklist",decklist);
            Commands.Add("deckfilteron", filterbyDeckonly);
            Commands.Add("deckfilteroff", disableDeckfiltering);
            Commands.Add("badmappers",mapperBlacklist);
            Commands.Add("mapperblacklist",mapperBlacklist);

            mapperWhitelist(TwitchWebSocketClient.OurTwitchUser,"mapper");
            loaddecks (TwitchWebSocketClient.OurTwitchUser,"");

#endif
        }


        private void lookup(TwitchUser requestor, string request)
        {
            if (!requestor.isMod && !requestor.isBroadcaster && !requestor.isSub)
            {
                QueueChatMessage($"lookup command is limited to Subscribers and moderators.");
                return;
            }

            StartCoroutine(ListSongs(requestor, request));

        }


        public static void Blacklist(int index)
        {
            // Add the song to the blacklist
            SongRequest request = FinalRequestQueue.ElementAt(index);
            JSONObject song = request.song;
            _songBlacklist.Add(request.song["id"]);
            Config.Instance.Blacklist = _songBlacklist;

            Instance.QueueChatMessage($"{song["songName"].Value} by {song["authorName"].Value} ({song["version"].Value}) is now blacklisted!");

            // Then skip the request
            Skip(index);
        }





        public static void Blacklist(SongRequest request)
        {

            // Add the song to the blacklist
            JSONObject song = request.song;
            _songBlacklist.Add(request.song["id"]);
            Config.Instance.Blacklist = _songBlacklist;

            Instance.QueueChatMessage($"{song["songName"].Value} by {song["authorName"].Value} ({song["version"].Value}) is now blacklisted!");


            // Then skip the request
        }

        public static void Skip(int index)
        {
            // Remove the song from the queue, then update the request button
            SongRequest request = FinalRequestQueue.ElementAt(index);
            TwitchUser requestor = request.requestor;
            if (_requestTracker.ContainsKey(requestor.id)) _requestTracker[requestor.id].numRequests--;
            FinalRequestQueue.RemoveAt(index);
            UpdateRequestButton();
            SongRequestDequeued?.Invoke(request.song);

            // Dismiss the request queue menu if there      are no more song requests
            if (FinalRequestQueue.Count == 0 && _songRequestMenu.customFlowCoordinator.isActivated)
            {
                _songRequestMenu.Dismiss();
            }
        }


        private bool filtersong(ref JSONObject song)
        {
            string songid = song["id"].Value;
            if (_songBlacklist.Contains(songid)) return true;
            if (duplicatelist.Contains(songid)) return true;
            return false;
        }


        private void restoredeck(TwitchUser requestor, string request)
        {
            Readdeck(requestor, "savedqueue");
        }


        private void mapperWhitelist(TwitchUser requestor, string request)
        {
            if (!requestor.isBroadcaster) return;


            if (request == "")
            {
                QueueChatMessage("usage: mapperwhitelist <on>,<off>,<clear> or name of mapper file.");
                return;
            }


            if (request == "on")
            {
                QueueChatMessage("Only approved mapper songs are now allowed.");
                mapperwhiteliston = true;
                return;
            }

            if (request == "off")
            {
                QueueChatMessage("Mapper whitelist is disabled.");
                mapperwhiteliston = false;
                return;
            }

            if (request == "clear")
            {
                QueueChatMessage("Mapper whitelist is now cleared.");

                mapperwhitelist.Clear();
                return;
            }

            string queuefile = $"{Environment.CurrentDirectory}\\requestqueue\\" + request + ".list";

            string fileContent = File.ReadAllText(queuefile);

            string[] Strings = fileContent.Split(new char[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);


            string whitelist = "Permitted mappers: ";
            foreach (string mapper in Strings)
            {
                mapperwhitelist.Add(mapper.ToLower());
                whitelist += mapper + " ";
            }

            if (mapperwhitelist.Count > 0) QueueChatMessage(whitelist);

        }

        // Not super efficient, but what can you do

        private bool mapperfiltered(ref JSONObject song)
        {
            string normalizedauthor = song["authorName"].Value.ToLower();

            if (mapperwhitelist.Count > 0)
            {
                foreach (var mapper in mapperwhitelist)
                {
                    if (normalizedauthor.Contains(mapper)) return false;
                }
                return true;
            }

            foreach (var mapper in mapperblacklist)
            {
                if (normalizedauthor.Contains(mapper)) return true;
            }

            return false;
        }


        private void mapperBlacklist(TwitchUser requestor, string request)
        {
            if (!requestor.isBroadcaster) return;

            if (request == "")
            {
                QueueChatMessage("usage: mapperblacklist <on>,<off>,<clear> or name of mapper file.");
                return;
            }

            if (request == "on")
            {
                QueueChatMessage("Songs with known bad mappers are disabled.");
                mapperblackliston = true;
                return;
            }

            if (request == "off")
            {
                QueueChatMessage("Bad mapper filtering is disabled.");
                mapperblackliston = false;
                return;
            }

            if (request == "clear")
            {
                QueueChatMessage("Bad mapper list is now cleared.");
                mapperblacklist.Clear();
                return;
            }

            string queuefile = $"{Environment.CurrentDirectory}\\requestqueue\\" + request + ".list";

            string fileContent = File.ReadAllText(queuefile);

            string[] Strings = fileContent.Split(new char[] { ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            string blacklist = "Mapper blacklist: ";
            foreach (string mapper in Strings)
            {
                mapperblacklist.Add(mapper.ToLower());
                blacklist += mapper + " ";
            }

            if (mapperblacklist.Count > 0) QueueChatMessage(blacklist);
        }




        private void showCommandlist(TwitchUser requestor, string request)
        {
            if (!requestor.isBroadcaster && !requestor.isMod) return;

            string commands = "";
            foreach (var item in Commands)
            {
                if (deck.ContainsKey(item.Key)) continue;  // Do not show deck names

                commands += "!" + item.Key + " ";
            }

            QueueChatMessage(commands);
        }


        private void filterbyDeckonly(TwitchUser requestor, string request)
        {
            if (!requestor.isBroadcaster) return;


            whitelist.Clear();


            foreach (var item in deck)
            {
                try
                {
                    string[] deckcontent = item.Value.Split(new char[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var entry in deckcontent)
                    {
                        whitelist.Add(entry);
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }

            whiteliston = true;

            QueueChatMessage($"Requests now limited to {whitelist.Count} songs in loaded decks.");

        }


        private void disableDeckfiltering(TwitchUser requestor, string request)
        {

            QueueChatMessage($"Requests no longer limited to loaded decks.");
            whiteliston = false;
        }

        private IEnumerator ListSongs(TwitchUser requestor, string request)
        {

            bool isBeatSaverId = _digitRegex.IsMatch(request) || _beatSaverRegex.IsMatch(request);

            string requestUrl = isBeatSaverId ? "https://beatsaver.com/api/songs/detail" : "https://beatsaver.com/api/songs/search/song";
            using (var web = UnityWebRequest.Get($"{requestUrl}/{request}"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"Error {web.error} occured when trying to request song {request}!");
                    QueueChatMessage($"Invalid BeatSaver ID \"{request}\" specified.");

                    yield break;
                }

                JSONNode result = JSON.Parse(web.downloadHandler.text);
                if (result["songs"].IsArray && result["total"].AsInt == 0)
                {
                    QueueChatMessage($"No results found for request \"{request}\"");

                    yield break;
                }
                JSONObject song;

                string songlist = "";


                if (result["songs"].IsArray)
                {
                    int count = 0;
                    foreach (JSONObject entry in result["songs"])
                    {
                        song = entry;
                        string songdetail = $"{song["songName"].Value}-{song["songSubName"].Value}-{song["authorName"].Value} ({song["version"].Value})";
                        //QueueChatMessage($"{song["songName"].Value} by {song["authorName"].Value} (#{song["id"]})");

                        if (songlist.Length + songdetail.Length > 498) break;
                        if (count > 0) songlist += ", ";
                        songlist += songdetail;
                        count++;

                    }

                }
                else
                {
                    song = result["song"].AsObject;
                    songlist += $"{song["songName"].Value}-{song["songSubName"].Value}-{song["authorName"].Value} ({song["version"].Value})";

                    //QueueChatMessage($"{song["songName"].Value} by {song["authorName"].Value} (#{song["id"]})");
                }

                QueueChatMessage(songlist);


                yield return null;

            }
        }


        /*
        |--------------------------------------------------------------------------
        | API Routes
        |--------------------------------------------------------------------------
        |
        | Here is where you can register API routes for your application. These
        | routes are loaded by the RouteServiceProvider within a group which
        | is assigned the "api" middleware group. Enjoy building your API!
        |
        Route::get('/songs/top/{start?}','ApiController@topDownloads');
        Route::get('/songs/plays/{start?}','ApiController@topPlayed');
        Route::get('/songs/new/{start?}','ApiController@newest');
        Route::get('/songs/rated/{start?}','ApiController@topRated');
        Route::get('/songs/byuser/{id}/{start?}','ApiController@byUser');
        Route::get('/songs/detail/{key}','ApiController@detail');
        Route::get('/songs/vote/{key}/{type}/{accessToken}', 'ApiController@vote');
        //Route::post('/songs/vote/{key}','ApiController@vote'); // @todo use post instead of get
        Route::get('/songs/search/{type}/{key}','ApiController@search');
        */

        private void addNewSongs(TwitchUser requestor, string request)
        {
            if (!requestor.isBroadcaster && !requestor.isMod)
            {
                QueueChatMessage($"addnewsongs command is limited to moderator.");
                return;
            }

            StartCoroutine(addsongsFromnewest(requestor, request));

        }

        private IEnumerator addsongsFromnewest(TwitchUser requestor, string request)
        {

            int totalSongs = 0;


            string requestUrl = "https://beatsaver.com/api/songs/new";

            int offset = 0;

            bool found = true;

            while (found && offset < 80)
            {
                found = false;


                using (var web = UnityWebRequest.Get($"{requestUrl}/{offset}"))
                {
                    yield return web.SendWebRequest();
                    if (web.isNetworkError || web.isHttpError)
                    {
                        Plugin.Log($"Error {web.error} occured when trying to request song {requestUrl}!");
                        QueueChatMessage($"Invalid BeatSaver ID \"{requestUrl}\" specified.");

                        yield break;
                    }

                    JSONNode result = JSON.Parse(web.downloadHandler.text);
                    if (result["songs"].IsArray && result["total"].AsInt == 0)
                    {
                        QueueChatMessage($"No results found for request \"{requestUrl}\"");

                        yield break;
                    }
                    JSONObject song;

                    string songlist = "";


                    if (result["songs"].IsArray)
                    {
                        int count = 0;
                        foreach (JSONObject entry in result["songs"])
                        {
                            found = true;
                            song = entry;
                            if (count > 0) songlist += ", ";

                            if (mapperfiltered(ref song)) continue;
                            if (filtersong(ref song)) continue;
                            ProcessSongRequest(requestor, song["version"].Value);
                            count++;
                            totalSongs++; ;
                        }

                    }
                    else
                    {
                        song = result["song"].AsObject;
                        songlist += $"{song["songName"].Value}-{song["songSubName"].Value}-{song["authorName"].Value} ({song["version"].Value})";

                        ProcessSongRequest(requestor, song["version"].Value);
                        totalSongs++;
                    }


                }


                offset += 20;
                if (totalSongs > 20) break;

            }

            if (totalSongs == 0)
            {
                QueueChatMessage($"No new songs found.");
            }

            yield return null;


        }

        private IEnumerator addsongsBymapper(TwitchUser requestor, string request)
        {
            int totalSongs = 0;


            string mapperid = "";

            using (var web = UnityWebRequest.Get($"https://beatsaver.com/api/songs/search/user/{request}"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"Error {web.error} occured when trying to request song {request}!");
                    QueueChatMessage($"Invalid BeatSaver ID \"{request}\" specified.");

                    yield break;
                }

                JSONNode result = JSON.Parse(web.downloadHandler.text);
                if (result["songs"].IsArray && result["total"].AsInt == 0)
                {
                    QueueChatMessage($"No results found for request \"{request}\"");
                    yield break;
                }

                foreach (JSONObject song in result["songs"].AsArray)
                {
                    mapperid = song["uploaderId"].Value;
                    break;
                }


                if (mapperid == "")
                {
                    QueueChatMessage($"Unable to find mapper {request}");
                    yield break;
                }

            }


            int offset = 0;

            string requestUrl = "https://beatsaver.com/api/songs/byuser";

            bool found = true;

            while (found)
            {
                found = false;

                using (var web = UnityWebRequest.Get($"{requestUrl}/{mapperid}/{offset}"))
                {
                    yield return web.SendWebRequest();
                    if (web.isNetworkError || web.isHttpError)
                    {
                        Plugin.Log($"Error {web.error} occured when trying to request song {request}!");
                        QueueChatMessage($"Invalid BeatSaver ID \"{request}\" specified.");

                        yield break;
                    }

                    JSONNode result = JSON.Parse(web.downloadHandler.text);
                    if (result["songs"].IsArray && result["total"].AsInt == 0)
                    {
                        QueueChatMessage($"No results found for request \"{request}\"");

                        yield break;
                    }
                    JSONObject song;

                    string songlist = "";


                    int count = 0;
                    foreach (JSONObject entry in result["songs"])
                    {
                        song = entry;
                        if (count > 0) songlist += ", ";
                        ProcessSongRequest(requestor, song["version"].Value);
                        count++;
                        found = true;
                        totalSongs++; ;
                    }

             
                }
                offset += 20;
            }

            //QueueChatMessage($"Added {totalSongs} songs.");

            yield return null;


        }

        // General search version
        private IEnumerator addsongs(TwitchUser requestor, string request)
        {

            int totalSongs = 0;


            bool isBeatSaverId = _digitRegex.IsMatch(request) || _beatSaverRegex.IsMatch(request);

            string requestUrl = isBeatSaverId ? "https://beatsaver.com/api/songs/detail" : "https://beatsaver.com/api/songs/search/song";


            using (var web = UnityWebRequest.Get($"{requestUrl}/{request}"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"Error {web.error} occured when trying to request song {request}!");
                    QueueChatMessage($"Invalid BeatSaver ID \"{request}\" specified.");

                    yield break;
                }

                JSONNode result = JSON.Parse(web.downloadHandler.text);
                if (result["songs"].IsArray && result["total"].AsInt == 0)
                {
                    QueueChatMessage($"No results found for request \"{request}\"");

                    yield break;
                }
                JSONObject song;

                string songlist = "";


                if (result["songs"].IsArray)
                {
                    int count = 0;
                    foreach (JSONObject entry in result["songs"])
                    {
                        song = entry;
                        if (count > 0) songlist += ", ";

                        if (filtersong(ref song)) continue;
                        ProcessSongRequest(requestor, song["version"].Value);
                        count++;
                        totalSongs++; ;
                    }

                }
                else
                {
                    song = result["song"].AsObject;
                    songlist += $"{song["songName"].Value}-{song["songSubName"].Value}-{song["authorName"].Value} ({song["version"].Value})";

                    ProcessSongRequest(requestor, song["version"].Value);
                    totalSongs++;
                }



                yield return null;

            }

            //QueueChatMessage($"Added {totalSongs} songs.");

        }




        private void addsongsbymapper(TwitchUser requestor, string request)
        {
            if (!requestor.isBroadcaster)
            {
                QueueChatMessage($"add mapper command is limited to the broadcaster.");
                return;

            }

            StartCoroutine(addsongsBymapper(requestor, request));

        }

        private void addSongs(TwitchUser requestor, string request)
        {
            if (!requestor.isBroadcaster)
            {
                QueueChatMessage($"add songs command is limited to the broadcaster.");
                return;

            }

            StartCoroutine(addsongs(requestor, request));

        }




        private void Remap(TwitchUser requestor, string request)
        {
            if (!requestor.isMod && !requestor.isBroadcaster) return;


            string[] parts = request.Split(',', ' ');

            if (parts.Length < 2)
            {
                QueueChatMessage("usage: !remap <songid>,<songid>, omit the <>'s");
                return;
            }


            songremap.Add(parts[0], parts[1]);
            QueueChatMessage($"Song {parts[0]} remapped to {parts[1]}");
            WriteRemapList();
        }


        private void Unmap(TwitchUser requestor, string request)
        {
            if (!requestor.isMod && !requestor.isBroadcaster) return;

            if (songremap.ContainsKey(request))
            {
                QueueChatMessage($"Remap entry {request} removed.");
                songremap.Remove(request);
            }
            WriteRemapList();
        }


        private void WriteRemapList()
        {
            //string remapfile = $"c:\\beatsaber\\remap.list";

            try
            {
                string remapfile = $"{Environment.CurrentDirectory}\\requestqueue\\remap.list";

                StreamWriter fileWriter = new StreamWriter(remapfile);

                foreach (var entry in songremap)
                {
                    fileWriter.Write($"{entry.Key},{entry.Value}\n");
                }

                fileWriter.Close();
            }
        catch   
            {

            }
        }

        private void ReadRemapList()
        {
            string remapfile = $"{Environment.CurrentDirectory}\\requestqueue\\remap.list";

            try
            {
                string fileContent = File.ReadAllText(remapfile);

                string[] maps = fileContent.Split('\r', '\n');
                for (int i = 0; i < maps.Length; i++)
                {
                    string[] parts = maps[i].Split(',', ' ');
                    if (parts.Length > 1) songremap.Add(parts[0], parts[1]);
                }

            }
            catch
            {
            }
        }

        private void ListQueue(TwitchUser requestor, string request)
        {

            int count = 0;
            var queuetext = "Queue: ";
            foreach (SongRequest req in FinalRequestQueue.ToArray())
            {
                var song = req.song;

                string songdetail = song["songName"].Value + " (" + song["version"] + ")";

                if (queuetext.Length + songdetail.Length > 498)
                {
                    QueueChatMessage(queuetext);
                    queuetext = "";
                }

                if (count > 0) queuetext += " , ";
                queuetext += songdetail;
                count++;
            }

            if (count == 0) queuetext = "Queue is empty.";
            QueueChatMessage(queuetext);

        }


        private void showSongsplayed(TwitchUser requestor, string request)
        {

            if (played.Count == 0)
            {
                QueueChatMessage("No songs have been played.");
                return;
            }

            int count = 0;
            var queuetext = $"{played.Count} songs played this session: ";
            foreach (JSONObject song in played)
            {

                string songdetail = song["songName"].Value + " (" + song["version"] + ")";

                if (queuetext.Length + songdetail.Length > 498)
                {
                    QueueChatMessage(queuetext);
                    queuetext = "";
                }

                if (count > 0) queuetext += " , ";
                queuetext += songdetail;
                count++;
            }

            QueueChatMessage(queuetext);

        }

        private static void writequeuesummarytofile()
        {

#if !PRIVATE
            return;
#endif

            try
            {

                int count = 0;
                string statusfile = $"{Environment.CurrentDirectory}\\requestqueue\\queuelist.txt";
                StreamWriter fileWriter = new StreamWriter(statusfile);

                string queuelist = "";

                foreach (SongRequest req in FinalRequestQueue.ToArray())
                {
                    var song = req.song;
                    queuelist += $"{song["songName"].Value}\n";
                    count++;
                    if (count > 8)
                    {
                        queuelist += "...\n";
                        break;
                    }
                }



                if (count == 0)
                    fileWriter.WriteLine("Queue is empty.");
                else
                    fileWriter.Write(queuelist);

                fileWriter.Close();
            }
        catch       
            {

            }

        }

        // Does songdescription contain request strings
        bool doescontain(string songdescription, string request)
        {
            string normalized = songdescription.ToLower();
            string[] words = request.ToLower().Split(new char[] { ' ', '\t' });
            if (words[0] == "") return false;
            foreach (string word in words)
            {
                if (!normalized.Contains(word)) return false;
            }
            return true;
        }

        public static void Writequeuestatustofile(string status)
        {
            //#if !PRIVATE
            //return;
            //#endif 

        try
            {


            string statusfile = $"{Environment.CurrentDirectory}\\requestqueue\\queuestatus.txt";
            StreamWriter fileWriter = new StreamWriter(statusfile);
            fileWriter.Write(status);
            fileWriter.Close();
            }

        catch
            {

            }

        }


        private void Writedeck(TwitchUser requestor, string request)
        {
            if (!requestor.isBroadcaster && request != "savedqueue") return;

            try
            {

                if (!_alphanumeric.IsMatch(request))
                    {
                    QueueChatMessage("usage: writedeck <alphanumeric deck name>");
                    return;
                    }

                int count = 0;

                if (FinalRequestQueue.Count == 0)
                {
                    QueueChatMessage("Queue is empty.");
                    return;
                }

                string queuefile = $"{Environment.CurrentDirectory}\\requestqueue\\" + request + ".deck";

                StreamWriter fileWriter = new StreamWriter(queuefile);

                foreach (SongRequest req in FinalRequestQueue.ToArray())
                {
                    var song = req.song;
                    if (count > 0) fileWriter.Write(",");
                    fileWriter.Write(song["id"].Value);
                    count++;
                }

                fileWriter.Close();

                if (request != "savedqueue") QueueChatMessage($"wrote {count} entries to {request}");
            }
            catch
            {
                QueueChatMessage("Was unable to write {queuefile}.");
            }
        }

        private void Readdeck(TwitchUser requestor, string request)
        {
            if (!requestor.isBroadcaster) return;

            try
            {



                string queuefile = $"{Environment.CurrentDirectory}\\requestqueue\\" + request + ".deck";

                string fileContent = File.ReadAllText(queuefile);

                string[] integerStrings = fileContent.Split(new char[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                int[] integers = new int[integerStrings.Length];

                for (int n = 0; n < integerStrings.Length; n++)
                {
                    integers[n] = int.Parse(integerStrings[n]);
                    ProcessSongRequest(requestor, integerStrings[n]);
                }

            }
        catch       
            {
            QueueChatMessage("Unable to read deck {request}.");
            }
        }


        private void showBanlist(TwitchUser requestor, string request)
        {

            if (!requestor.isMod && !requestor.isBroadcaster) return;

            int count = 0;
            var queuetext = "Banlist: ";
            foreach (string req in _songBlacklist.ToArray())
            {

                if (queuetext.Length + req.Length > 480)
                {
                    QueueChatMessage(queuetext);
                    queuetext = "";
                }
                else if (count > 0) queuetext += " , ";

                queuetext += req;
                count++;
            }

            if (count == 0) queuetext = "Banlist is empty.";
            QueueChatMessage(queuetext);
        }

        private void ListPlayedlist(TwitchUser requestor, string request)
        {

            if (!requestor.isMod && !requestor.isBroadcaster) return;

            int count = 0;
            var queuetext = "Requested this session: ";
            foreach (string req in duplicatelist.ToArray())
            {


                if (queuetext.Length + req.Length > 480)
                {
                    QueueChatMessage(queuetext);
                    queuetext = "";
                }
                else if (count > 0) queuetext += " , ";

                queuetext += req;
                count++;
            }

            if (count == 0) queuetext = "Played list is empty.";
            QueueChatMessage(queuetext);
        }





        private void UnBan(TwitchUser requestor, string request)
        {

            if (!requestor.isMod && !requestor.isBroadcaster) return;


            var unbanvalue = getid(request);


            if (unbanvalue == "")
            {
                QueueChatMessage($"usage: !unblock <songid>, omit <>'s");
                return;
            }

            if (_songBlacklist.Contains(unbanvalue))
            {
                QueueChatMessage($"Removed {request} from the blacklist.");
                _songBlacklist.Remove(unbanvalue);
                Config.Instance.Blacklist = _songBlacklist;

            }
            else
            {
                QueueChatMessage($"{request} is not on the blacklist.");
            }

        }


        private void Block(TwitchUser requestor, string request)
        {

            if (!requestor.isMod && !requestor.isBroadcaster) return;


            var banvalue = getid(request);

            if (banvalue == "")
            {
                QueueChatMessage($"usage: !block <songid>, omit <>'s.");
                return;
            }


            if (_songBlacklist.Contains(banvalue))
            {
                QueueChatMessage($"{request} is already on the blacklist.");
            }
            else
            {
                QueueChatMessage($"{request} added to the blacklist.");
                _songBlacklist.Add(banvalue);
                Config.Instance.Blacklist = _songBlacklist;

            }

        }

        private void WrongSong(TwitchUser requestor, string request)
        {

            for (int i = FinalRequestQueue.Count - 1; i >= 0; i--)
            {
                var song = FinalRequestQueue[i].song;
                if (FinalRequestQueue[i].requestor.id == requestor.id)
                {
                    QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) removed.");
                    RequestBot.Skip(i);
                    return;

                }
            }

            QueueChatMessage($"You have no requests in the queue.");

        }



        private void Unqueuesong(TwitchUser requestor, string request)
        {

            if (!requestor.isMod && !requestor.isBroadcaster) return;


            var uniqueid = getid(request);
            if (request == "")
            {
                QueueChatMessage($"Usage: !remove <song>, omit <>'s.");
                return;
            }


            for (int i = FinalRequestQueue.Count - 1; i >= 0; i--)
            {
                var song = FinalRequestQueue[i].song;

                if (uniqueid == "")
                {
                    if (doescontain($"{song["songName"].Value} {song["songSubName"].Value} {song["authorName"].Value} {song["version"].Value} {FinalRequestQueue[i].requestor.displayName}", request))
                    {
                        QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) removed.");
                        RequestBot.Skip(i);
                        return;
                    }
                }
                else
                {
                    if (song["id"].Value == uniqueid)
                    {
                        QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) removed.");
                        RequestBot.Skip(i);
                        return;
                    }
                }
            }

            QueueChatMessage($"{request} was not found in the queue.");

        }


        string getid(string request)
        {
            if (_digitRegex.IsMatch(request)) return request;
            if (_beatSaverRegex.IsMatch(request))
            {
                string[] requestparts = request.Split(new char[] { '-' }, 2);
                return requestparts[0];
            }
            return "";
        }


        private void mtt(TwitchUser requestor, string request)
        {

            if (!requestor.isMod && !requestor.isBroadcaster) return;

            string unqueueid = getid(request);


            if (request == "")
            {
                QueueChatMessage($"usage: !mtt <song id> , omit <>'s.");
                return;
            }

            for (int i = FinalRequestQueue.Count - 1; i >= 0; i--)
            {
                var song = FinalRequestQueue[i].song;

                if (unqueueid == "")
                {
                    if (doescontain($"{song["songName"].Value} {song["songSubName"].Value} {song["authorName"].Value} {song["version"].Value} {FinalRequestQueue[i].requestor.displayName}", request))
                    {

                        SongRequest req = FinalRequestQueue.ElementAt(i);
                        FinalRequestQueue.RemoveAt(i);
                        FinalRequestQueue.Insert(0, req);
                        SongRequestQueued?.Invoke(song);

                        writequeuesummarytofile();

                        QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) promoted.");
                        return;
                    }
                }
                else
                {



                    if (song["id"].Value == unqueueid)
                    {

                        SongRequest req = FinalRequestQueue.ElementAt(i);
                        FinalRequestQueue.RemoveAt(i);
                        FinalRequestQueue.Insert(0, req);
                        SongRequestQueued?.Invoke(song);

                        writequeuesummarytofile();

                        QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) promoted.");
                        return;


                    }
                }
            }

            QueueChatMessage($"{request} was not found in the queue.");

        }


        private void last(TwitchUser requestor, string request)
        {

            if (!requestor.isMod && !requestor.isBroadcaster) return;

            string unqueueid = getid(request);


            if (request == "")
            {
                QueueChatMessage($"usage: !last <song id> , omit <>'s.");
                return;
            }

            for (int i = FinalRequestQueue.Count - 1; i >= 0; i--)
            {
                var song = FinalRequestQueue[i].song;

                if (unqueueid == "")
                {
                    if (doescontain($"{song["songName"].Value} {song["songSubName"].Value} {song["authorName"].Value} {song["version"].Value} {FinalRequestQueue[i].requestor.displayName}", request))
                    {

                        SongRequest req = FinalRequestQueue.ElementAt(i);
                        FinalRequestQueue.RemoveAt(i);
                        FinalRequestQueue.Add(req);
                        SongRequestQueued?.Invoke(song);

                        writequeuesummarytofile();

                        QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) demoted.");
                        return;
                    }
                }
                else
                {



                    if (song["id"].Value == unqueueid)
                    {

                        SongRequest req = FinalRequestQueue.ElementAt(i);
                        FinalRequestQueue.RemoveAt(i);
                        FinalRequestQueue.Add(req);
                        SongRequestQueued?.Invoke(song);

                        writequeuesummarytofile();

                        QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) demoted.");
                        return;


                    }
                }
            }

            QueueChatMessage($"{request} was not found in the queue.");
        }

        private void Clearqueue(TwitchUser requestor, string request)
        {
            if (!requestor.isBroadcaster) return;

            Writedeck(requestor, "justcleared");

            foreach (var song in FinalRequestQueue)
                {
                SongRequestHistory.Insert(0, song);

                }

            _persistentRequestQueue.Clear();
            Config.Instance.RequestQueue = _persistentRequestQueue;

            FinalRequestQueue.Clear();
            UpdateRequestButton();
            if (FinalRequestQueue.Count == 0 && _songRequestMenu.customFlowCoordinator.isActivated)
                _songRequestMenu.Dismiss();


            QueueChatMessage($"Queue is now empty.");


        }



        private void ProcessSongRequest(TwitchUser requestor, string request)
        {

            try
            {
                if (QueueOpen == false && !requestor.isBroadcaster && !requestor.isMod)
                {
                    Commands["usermessage"].Invoke(requestor, "Queue is currently closed.");
                    return;
                }

                if (request == "")
                {
                    QueueChatMessage($"usage: bsr <song id> or <part of song name and mapper if known>");
                    return;
                }

                if (!_requestTracker.ContainsKey(requestor.id))
                    _requestTracker.Add(requestor.id, new RequestUserTracker());


                int limit = Config.Instance.RequestLimit;
                if (requestor.isSub) limit = Math.Max(limit, Config.Instance.SubRequestLimit);
                if (requestor.isMod) limit = Math.Max(limit, Config.Instance.ModRequestLimit);
                if (requestor.isVip) limit++; // Treated as a bonus. Still being finalized

                // Currently using simultaneous request limits, will be introduced later / or activated if time mode is on.

                /*
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
                */


                // Only rate limit users who aren't mods or the broadcaster
                if (!requestor.isBroadcaster)
                {

                    //if (_requestTracker[requestor.id].resetTime <= DateTime.Now)
                    //{
                    //  _requestTracker[requestor.id].resetTime = DateTime.Now.AddMinutes(Config.Instance.RequestCooldownMinutes);
                    //  _requestTracker[requestor.id].numRequests = 0;
                    //}
                    if (_requestTracker[requestor.id].numRequests >= limit)
                    {
                        QueueChatMessage($"You already have {_requestTracker[requestor.id].numRequests} on the queue. You can add another once one is played. Subscribers are limited to {Config.Instance.SubRequestLimit}.");
                        //  var time = (_requestTracker[requestor.id].resetTime - DateTime.Now);
                        //QueueChatMessage($"{requestor.displayName}, you can make another request in{(time.Minutes > 0 ? $" {time.Minutes} minute{(time.Minutes > 1 ? "s" : "")}" : "")} {time.Seconds} second{(time.Seconds > 1 ? "s" : "")}.");
                        return;
                    }
                }



                RequestInfo newRequest = new RequestInfo(requestor, request, DateTime.UtcNow, _digitRegex.IsMatch(request) || _beatSaverRegex.IsMatch(request));
                if (!newRequest.isBeatSaverId && request.Length < 3)
                    Instance.QueueChatMessage($"Request \"{request}\" is too short- Beat Saver searches must be at least 3 characters!");
                else if (!UnverifiedRequestQueue.Contains(newRequest))
                    UnverifiedRequestQueue.Enqueue(newRequest);

            }
            catch (Exception e)
            {
                QueueChatMessage($"Exception was caught when trying to process add. {e.ToString()}");

            }
        }


        private void openQueue(TwitchUser requestor, string request)
        {
            if (!requestor.isMod && !requestor.isBroadcaster) return;

            QueueOpen = true;
            QueueChatMessage("Queue is now open.");
            Writequeuestatustofile("Queue is open");

        }

        private void closeQueue(TwitchUser requestor, string request)
        {
            if (!requestor.isMod && !requestor.isBroadcaster) return;

            QueueOpen = false;
            QueueChatMessage("Queue is now closed.");
            Writequeuestatustofile("Queue is closed");
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

            string key = parts[0].Substring(1).ToLower();

            // Execute custom deck commands as if they were normal commands

            if (deck.ContainsKey(key))
            {
                string param = key;
                if (parts.Length > 1) param += " " + parts[1];
                Commands[key]?.Invoke(user, param);
            }


            // Allow command with no parameters
            if (parts.Length == 1)
            {
                Commands[key]?.Invoke(user, "");
                return;
            }


            string command = parts[0].Substring(1)?.ToLower();
            if (Commands.ContainsKey(command))
                Commands[command]?.Invoke(user, parts[1]);
        }



    }
}



