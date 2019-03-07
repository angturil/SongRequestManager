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

        private static readonly Regex _digitRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _beatSaverRegex = new Regex("^[0-9]+-[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _alphaNumericRegex = new Regex("^[0-9A-Za-z]+$", RegexOptions.Compiled);
        private static readonly Regex _RemapRegex = new Regex("^[0-9]+,[0-9]+$", RegexOptions.Compiled);

        private static readonly Regex _beatsaversong=new Regex("^[0-9]+$|^[0-9]+-[0-9]+$", RegexOptions.Compiled);

        private static readonly Regex _nothing = new Regex("$^", RegexOptions.Compiled);
        private static readonly Regex _anything = new Regex(".*", RegexOptions.Compiled); // Is this the most efficient way?

        public static RequestBot Instance;
        public static ConcurrentQueue<RequestInfo> UnverifiedRequestQueue = new ConcurrentQueue<RequestInfo>();
        public static ConcurrentQueue<KeyValuePair<SongRequest, bool>> BlacklistQueue = new ConcurrentQueue<KeyValuePair<SongRequest, bool>>();
        public static Dictionary<string, RequestUserTracker> RequestTracker = new Dictionary<string, RequestUserTracker>();

        private static Button _requestButton;
        private static bool _refreshQueue = false;

        private static FlowCoordinator _levelSelectionFlowCoordinator;
        private static DismissableNavigationController _levelSelectionNavigationController;
        private static Queue<string> _botMessageQueue = new Queue<string>();
        //private static Dictionary<string, Action<TwitchUser, string>> Commands = new Dictionary<string, Action<TwitchUser, string>>();

        private static Dictionary<string, BOTCOMMAND> NewCommands = new Dictionary<string, BOTCOMMAND>(); // This will replace command dictionary

        static public bool QueueOpen = false;
        bool mapperwhiteliston = false;
        bool mapperblackliston = false;

        private static System.Random generator = new System.Random();

        public static List<JSONObject> played = new List<JSONObject>(); // Played list

        private static StringListManager mapperwhitelist = new StringListManager(); // Lists because we need to interate them per song
        private static StringListManager mapperblacklist = new StringListManager(); // Lists because we need to interate them per song

        private static HashSet<string> duplicatelist = new HashSet<string>();
        private static Dictionary<string, string> songremap = new Dictionary<string, string>();
        private static Dictionary<string, string> deck = new Dictionary<string, string>(); // deck name/content

        public static string datapath;

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

            datapath = Path.Combine(Environment.CurrentDirectory, "UserData", "EnhancedTwitchChat");
            if (!Directory.Exists(datapath))
                Directory.CreateDirectory(datapath);

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

            RequestQueue.Read();
            RequestHistory.Read();
            SongBlacklist.Read();

            UpdateRequestButton();
            InitializeCommands();

            StartCoroutine(ProcessRequestQueue());
            StartCoroutine(ProcessBlacklistRequests());
        }

        private void FixedUpdate()
        {
            if (_botMessageQueue.Count > 0)
                SendChatMessage(_botMessageQueue.Dequeue());

            if (_refreshQueue)
            {
                if (RequestBotListViewController.Instance.isActivated)
                    RequestBotListViewController.Instance.UpdateRequestUI(true);
                _refreshQueue = false;
            }
        }

        private IEnumerator ProcessBlacklistRequests()
        {
            WaitUntil waitForBlacklistRequest = new WaitUntil(() => BlacklistQueue.Count > 0);
            while (!Plugin.Instance.IsApplicationExiting)
            {
                yield return waitForBlacklistRequest;

                if (BlacklistQueue.Count > 0 && BlacklistQueue.TryDequeue(out var request))
                {
                    bool silence = request.Value;
                    string songId = request.Key.song["id"].Value;
                    using (var web = UnityWebRequest.Get($"https://beatsaver.com/api/songs/detail/{songId}"))
                    {
                        yield return web.SendWebRequest();
                        if (web.isNetworkError || web.isHttpError)
                        {
                            if (!silence) QueueChatMessage($"Invalid BeatSaver ID \"{songId}\" specified.");
                            continue;
                        }

                        JSONNode result = JSON.Parse(web.downloadHandler.text);

                        if (result["songs"].IsArray && result["total"].AsInt == 0)
                        {
                            if (!silence) QueueChatMessage($"BeatSaver ID \"{songId}\" does not exist.");
                            continue;
                        }
                        yield return null;

                        request.Key.song = result["song"].AsObject;
                        SongBlacklist.Songs.Add(songId, request.Key);
                        SongBlacklist.Write();

                        if (!silence) QueueChatMessage($"{request.Key.song["songName"].Value} by {request.Key.song["authorName"].Value} ({songId}) added to the blacklist.");
                    }
                }
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

        public void QueueChatMessage(string message)
        {
            _botMessageQueue.Enqueue(message);
        }

        public static string GetStarRating(ref JSONObject song, bool mode = true)
        {
            if (!mode) return "";

            string stars = "******";
            float rating = song["rating"].AsFloat;
            if (rating < 0 || rating > 100) rating = 0;
            string starrating = stars.Substring(0, (int)(rating / 17)); // 17 is used to produce a 5 star rating from 80ish to 100.
            return starrating;
        }

        public static string GetRating(ref JSONObject song, bool mode = true)
        {
            if (!mode) return "";

            string rating = song["rating"].AsInt.ToString();
            if (rating == "0") return "";
            return rating+'%';

        }


        private IEnumerator ProcessRequestQueue()
        {
            var waitForRequests = new WaitUntil(() => UnverifiedRequestQueue.Count > 0);
            while (!Plugin.Instance.IsApplicationExiting)
            {
                yield return waitForRequests;

                if (UnverifiedRequestQueue.TryDequeue(out var requestInfo))
                    yield return CheckRequest(requestInfo);
            }
        }

        private IEnumerator CheckRequest(RequestInfo requestInfo)
        {
            TwitchUser requestor = requestInfo.requestor;

            string request = requestInfo.request;

            // Special code for numeric searches
            if (requestInfo.isBeatSaverId)
            {
                // Remap song id if entry present. This is one time, and not correct as a result. No recursion right now, could be confusing to the end user.
                string[] requestparts = request.Split(new char[] { '-' }, 2);

                if (requestparts.Length > 0 && songremap.ContainsKey(requestparts[0]) && isNotModerator(requestor))
                {
                    request = songremap[requestparts[0]];
                    QueueChatMessage($"Remapping request {requestInfo.request} to {request}");
                }

                string requestcheckmessage = IsRequestInQueue(request);               // Check if requested ID is in Queue  
                if (requestcheckmessage != "")
                {
                    QueueChatMessage(requestcheckmessage);
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
                    yield break;
                }

                JSONNode result = JSON.Parse(web.downloadHandler.text);

                if (result["songs"].IsArray && result["total"].AsInt == 0)
                {
                    QueueChatMessage($"No results found for request \"{request}\"");
                    yield break;
                }
                yield return null;

                List<JSONObject> songs = new List<JSONObject>();                 // Load resulting songs into a list 

                string errormessage = "";


                if (result["songs"].IsArray)
                {

                    // Might consider sorting the list by rating to improve quality of results            
                    foreach (JSONObject currentSong in result["songs"].AsArray)
                    {
                
                        errormessage = SongSearchFilter(currentSong, false);
                        if (errormessage == "")
                            songs.Add(currentSong);
                    }
                }
                else
                {
                    songs.Add(result["song"].AsObject);
                }


                // Filter out too many or too few results
                if (songs.Count == 0)
                {
                    if (errormessage == "") errormessage = $"No results found for request \"{request}\"";
                }
                else if (!Config.Instance.AutopickFirstSong && songs.Count >= 4)
                    errormessage = $"Request for '{request}' produces {songs.Count} results, narrow your search by adding a mapper name, or use https://beatsaver.com to look it up.";
                else if (!Config.Instance.AutopickFirstSong && songs.Count > 1 && songs.Count < 4)
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
                    QueueChatMessage(errormessage);
                    yield break;
                }

                var song = songs[0];

                RequestTracker[requestor.id].numRequests++;
                duplicatelist.Add(song["id"].Value);

                RequestQueue.Songs.Add(new SongRequest(song, requestor, requestInfo.requestTime, RequestStatus.Queued));
                RequestQueue.Write();

                Writedeck(requestor, "savedqueue"); // Might not be needed.. logic around saving and loading deck state needs to be reworked

                //QueueChatMessage($"Request {song["songName"].Value} by {song["authorName"].Value} {GetStarRating(ref song, Config.Instance.ShowStarRating)} ({song["version"].Value}) added to queue.");

                // We want to allow the end user to customize some of the bot messages to their own preferences. You can read the message text from a file.

                new DynamicText().AddSong(ref song).QueueMessage(Config.Instance.AddSongToQueueText);

                UpdateRequestButton();
                _refreshQueue = true;
            }
        }


        private static IEnumerator ProcessSongRequest(int index, bool fromHistory = false)
        {
            if ((RequestQueue.Songs.Count > 0 && !fromHistory) || (RequestHistory.Songs.Count > 0 && fromHistory))
            {
                SongRequest request = null;
                if (!fromHistory)
                {
                    SetRequestStatus(index, RequestStatus.Played);
                    request = DequeueRequest(index);
                }
                else
                {
                    request = RequestHistory.Songs.ElementAt(index);
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

                Instance.QueueChatMessage($"{song["songName"].Value} by {song["authorName"].Value} {GetSongLink(ref song, 2)} is next.");


                _songRequestMenu.Dismiss();
            }
        }


        public static string GetSongLink(ref JSONObject song, int formatindex)
        {
            string[] link ={
                    $"({song["version"].Value})",
                    $"https://beatsaver.com/browse/detail/{song["version"].Value}",
                    $"https://bsaber.com/songs/{song["id"].Value}"
                    };

            if (formatindex >= link.Length) return "";

            return link[formatindex];
        }

        private static void UpdateRequestButton()
        {
            try
            {
                RequestBot.WriteQueueSummaryToFile(); // Write out queue status to file, do it first

                if (RequestQueue.Songs.Count == 0)
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
            RequestHistory.Songs.Insert(0, request);
            if (RequestHistory.Songs.Count > Config.Instance.RequestHistoryLimit)
            {
                int diff = RequestHistory.Songs.Count - Config.Instance.RequestHistoryLimit;
                RequestHistory.Songs.RemoveRange(RequestHistory.Songs.Count - diff - 1, diff);
            }
            RequestQueue.Songs.Remove(request);
            RequestHistory.Write();
            RequestQueue.Write();

            // Decrement the requestors request count, since their request is now out of the queue
            if (RequestTracker.ContainsKey(request.requestor.id)) RequestTracker[request.requestor.id].numRequests--;

            UpdateRequestButton();
            _refreshQueue = true;
        }

        public static SongRequest DequeueRequest(int index)
        {
            SongRequest request = RequestQueue.Songs.ElementAt(index);

            if (request != null)
                DequeueRequest(request);
            return request;
        }

        public static void SetRequestStatus(int index, RequestStatus status, bool fromHistory = false)
        {
            if (!fromHistory)
                RequestQueue.Songs[index].status = status;
            else
                RequestHistory.Songs[index].status = status;
        }

        public static void Blacklist(int index, bool fromHistory, bool skip)
        {
            // Add the song to the blacklist
            SongRequest request = fromHistory ? RequestHistory.Songs.ElementAt(index) : RequestQueue.Songs.ElementAt(index);

            SongBlacklist.Songs.Add(request.song["id"].Value, new SongRequest(request.song, request.requestor, DateTime.UtcNow, RequestStatus.Blacklisted));
            SongBlacklist.Write();

            Instance.QueueChatMessage($"{request.song["songName"].Value} by {request.song["authorName"].Value} ({request.song["id"].Value}) added to the blacklist.");

            if (!fromHistory)
            {
                if (skip)
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
            Instance?.StartCoroutine(ProcessSongRequest(0));
        }


        private void InitializeCommands()
        {

            // Note: Default permissions are broadcaster only, so don't need to set them
            // These settings need to be able to reconstruct  

            // Note, this really should pass the alias list instead of adding 3 commmands.
            foreach (string c in Config.Instance.RequestCommandAliases.Split(',').Distinct())
            {
                AddCommand(c, ProcessSongRequest,Everyone,"usage: %alias <songname> or <song id>, omit <,>'s. %endusage This adds a song to the request queue. Try and be a little specific. You can look up songs on %beatsaver",_anything);
                Plugin.Log($"Added command alias \"{c}\" for song requests.");
            }
  
            // Testing prototype code now
            AddCommand("queue", ListQueue,Everyone,"usage: %alias %endusage  ... Displays a list of the currently requested songs.",_nothing);

            AddCommand("unblock", Unban,Mod,"usage: %alias <song id>, do not include <,>'s.",_beatsaversong);

            AddCommand("block", Ban,Mod,"usage: %alias <song id>, do not include <,>'s.",_beatsaversong);

            AddCommand("remove", DequeueSong,Mod, "usage: %alias <songname>,<username>,<song id> %endusage ... Removes a song from the queue.",_anything);

            AddCommand("clearqueue", Clearqueue,Broadcasteronly,"usage: %alias %endusage ... Clears the song request queue. You can still get it back from the JustCleared deck, or the history window",_nothing);

            AddCommand("mtt", MoveRequestToTop,Mod,"usage: %alias <songname>,<username>,<song id> %endusage ... Moves a song to the top of the request queue.",_anything );

            AddCommand("remap", Remap,Mod,"usage: %alias <songid1> , <songid2>%endusage ... Remaps future song requests of <songid1> to <songid2> , hopefully a newer/better version of the map.",_RemapRegex);

            AddCommand("unmap", Unmap,Mod,"usage: %alias <songid> %endusage ... Remove future remaps for songid.",_beatsaversong);

            AddCommand(new string [] { "lookup","find"}, lookup,Mod | Sub | VIP ,"usage: %alias <song name> or <beatsaber id>, omit <>'s.%endusage Get a list of songs from %beatsaver matching your search criteria.");

            AddCommand(new string[] { "last", "demote", "later" }, MoveRequestToBottom,Mod,"usage: %alias <songname>,<username>,<song id> %endusage ... Moves a song to the bottom of the request queue.", _anything);

            AddCommand(new string[] { "wrongsong", "wrong", "oops" }, WrongSong,Everyone,"usage: %alias %endusage ... Removes your last requested song form the queue. It can be requested again later.",_nothing);

            AddCommand("blist", ShowBanList,Broadcasteronly,"usage: Don't use, it will spam chat.",_nothing);

            AddCommand("open", OpenQueue,Mod,"usage: %alias %endusage ... Opens the queue allowing song requests.",_nothing);

            AddCommand("close", CloseQueue,Mod, "usage: %alias %endusage ... Closes the request queue.", _nothing);

            AddCommand("restore", restoredeck,Broadcasteronly,"usage: %alias %endusage ... Restores the request queue from the previous session. Only useful if you have persistent Queue turned off.",_nothing );

            AddCommand("commandlist", showCommandlist,Everyone,"usage: %alias %endusage ... Displays all the bot commands available to you.",_nothing);

            AddCommand("played", ShowSongsplayed,Mod,"usage: %alias %endusage ... Displays all the songs already played this session.", _nothing);

            AddCommand("readdeck", Readdeck);
            AddCommand("writedeck", Writedeck);

            AddCommand("clearalreadyplayed", ClearDuplicateList,Broadcasteronly,"usage: %alias %endusage ... clears the list of already requested songs, allowing them to be requested again.",_nothing); // Needs a better name

            AddCommand("help", help, Everyone, "usage: %alias <command name>, or just %alias to show a list of all commands available to you.",_anything);

            AddCommand("link", ShowSongLink,Everyone,"usage: %alias%endusage ... Shows details, and a link to the current song",_nothing);

            // Whitelists mappers and add new songs, this code is being refactored and transitioned to testing

            AddCommand("allowmappers", mapperWhitelist,Broadcasteronly,"usage: %alias <mapper list> %endusage ... Selects the mapper list used by the AddNew command for adding the latest songs from %beatsaver, filtered by the mapper list.",_alphaNumericRegex);  // The message needs better wording, but I don't feel like it right now
            AddCommand("blockmappers", mapperBlacklist,Broadcasteronly,"usage: %alias <mapper list> %endusage ... Selects a mapper list that will not be allowed in any song requests.", _alphaNumericRegex);

            AddCommand(new string[] { "addnew", "addlatest" }, addNewSongs,Mod,"usage: %alias %endusage ... Adds the latest maps from %beatsaver, filtered by the previous selected allowmappers command",_nothing); // BUG: Note, need something to get the one of the true commands referenced, incases its renamed
            AddCommand("addsongs", addSongs,Broadcasteronly); // Basically search all, need to decide if its useful

            // Temporary commands for testing
            AddCommand("load", LoadList);
            AddCommand("unload", UnloadList);
            AddCommand("clearlist", ClearList);
            AddCommand("write", writelist);
            AddCommand("list", ListList);
            AddCommand("lists", showlists);

#if PRIVATE
            AddCommand("deck",createdeck);
            AddCommand("unloaddeck",unloaddeck);      
            AddCommand("loaddecks",loaddecks);
            AddCommand("decklist",decklist);
            AddCommand("mapper", addsongsbymapper); // This is actually most useful if we send it straight to list

            loaddecks (TwitchWebSocketClient.OurTwitchUser,"");
#endif

            ReadRemapList();
            LoadList(TwitchWebSocketClient.OurTwitchUser, "mapper.list"); // BUG: There are 2 calls, will unify shortly
            mapperWhitelist(TwitchWebSocketClient.OurTwitchUser, "mapper.list");

        }

        private void lookup(TwitchUser requestor, string request)
        {
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
                if (QueueOpen == false && isNotModerator(requestor)) // BUG: Complex permission, Queue state message needs to be handled higher up
                {
                    QueueChatMessage($"Queue is currently closed.");
                    return;
                }

                // The help text is part of help now, and will be configurable

                if (!RequestTracker.ContainsKey(requestor.id))
                    RequestTracker.Add(requestor.id, new RequestUserTracker());

                int limit = Config.Instance.RequestLimit;
                if (requestor.isSub) limit = Math.Max(limit, Config.Instance.SubRequestLimit);
                if (requestor.isMod) limit = Math.Max(limit, Config.Instance.ModRequestLimit);
                if (requestor.isVip) limit += Config.Instance.VipBonus; // Current idea is to give VIP's a bonus over their base subscription class, you can set this to 0 if you like

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

                if (!requestor.isBroadcaster)
                {
                    if (RequestTracker[requestor.id].numRequests >= limit)
                    {
                        QueueChatMessage($"You already have {RequestTracker[requestor.id].numRequests} on the queue. You can add another once one is played. Subscribers are limited to {Config.Instance.SubRequestLimit}.");

                        // Custom text example. This one of the messages users likely want to be able to change.
                        // new DynamicText().Add("requests", RequestTracker[requestor.id].numRequests.ToString()).Add("RequestLimit", Config.Instance.SubRequestLimit.ToString()).QueueMessage("You already have %Requests on the queue.You can add another once one is played.Subscribers are limited to %RequestLimit.)");
 

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


        #region NEW Command Processor

        // This code probably needs its own file
        // Some of these are just ideas, putting them all down, can filter them out later
        [Flags]
        public enum CmdFlags
        {
            None = 0,
            Everyone = 1,
            Sub = 2,
            Mod = 4,
            Broadcaster = 8,
            VIP = 16,
            UserList = 32,  // If this is enabled, users on a list are allowed to use a command (this is an OR, so leave restrictions to Broadcaster if you want ONLY users on a list)
            TwitchLevel = 63, // This is used to show ONLY the twitch user flags when showing permissions

            ShowRestrictions = 64, // Using the command without the right access level will show permissions error. Mostly used for commands that can be unlocked at different tiers.
            UsageHelp = 128, // Enable usage help for blank / invalid command and ?
            LongHelp = 256, // Enable ? operation, showing a longer explanation in stream (try to limit it to one message)
            HelpLink = 512, // Enable link to web documentation

            WhisperReply = 1024, // Reply in a whisper to the user (future feature?). Allow commands to send the results to the user, avoiding channel spam

            Timeout = 2048, // Applies a timeout to regular users after a command is succesfully invoked this is just a concept atm
            TimeoutSub = 4096, // Applies a timeout to Subs
            TimeoutVIP = 8192, // Applies a timeout to VIP's
            TimeoutMod = 16384, // Applies a timeout to MOD's. A way to slow spamming of channel for overused commands. 

            NoLinks = 32768, // Turn off any links that the command may normally generate
            Silent = 65536, // Command produces no output at all - but still executes
            Verbose = 131072, // Turn off command output limits, This can result in excessive channel spam
            Log = 262144, // Log every use of the command to a file
            RegEx = 524288, // Enable regex check
            UserFlag1 = 1048576, // Use it for whatever bit makes you happy 
            UserFlag2 = 2097152, // Use it for whatever bit makes you happy 
            UserFlag3 = 4194304, // Use it for whatever bit makes you happy 
            UserFlag4 = 8388608, // Use it for whatever bit makes you happy 

            SilentPreflight = 16277216, //  

            Disabled = 1 << 30, // If ON, the command will not be added to the alias list at all.
        }

        const CmdFlags Default = CmdFlags.UsageHelp;
        const CmdFlags Everyone = Default | CmdFlags.Everyone;
        const CmdFlags Broadcasteronly = Default | CmdFlags.Broadcaster;
        const CmdFlags Mod = Default | CmdFlags.Broadcaster | CmdFlags.Mod;
        const CmdFlags Sub = Default | CmdFlags.Sub;
        const CmdFlags VIP = Default | CmdFlags.VIP;

        // Prototype code only
        public struct BOTCOMMAND
        {
            public Action<TwitchUser, string> Method;  // Method to call
            public CmdFlags cmdflags;                  // flags
            public string ShortHelp;                   // short help text (on failing preliminary check
            public List<string> aliases;               // list of command aliases
            public Regex regexfilter;                 // reg ex filter to apply. For now, we're going to use a single string

            public string LongHelp; // Long help text
            public string HelpLink; // Help website link
            StringListManager permittedusers; // List of users permitted to use the command, uses list manager.
            public string userparameter; // This is here incase I need it for some specific purpose

            public BOTCOMMAND(Action<TwitchUser, string> method, CmdFlags flags, string shorthelptext,Regex regex, string[] alias)
            {
                Method = method;
                cmdflags = flags;
                ShortHelp = shorthelptext;
                aliases = alias.ToList();
                LongHelp = "";
                HelpLink = "";
                permittedusers = null;
                if (regex == null)
                    regexfilter = _anything;
                else
                    regexfilter = regex;

                userparameter = "";


                foreach (var entry in aliases) NewCommands.Add(entry, this);
            }
        }

        public static List<BOTCOMMAND> cmdlist = new List<BOTCOMMAND>();

        public void AddCommand(string[] alias, Action<TwitchUser, string> method, CmdFlags flags = Broadcasteronly, string shorthelptext = "usage: [%alias] ... Rights: %rights",Regex regex=null)
        {
            cmdlist.Add(new BOTCOMMAND(method, flags, shorthelptext,regex, alias));
        }

        public void AddCommand(string alias, Action<TwitchUser, string> method, CmdFlags flags = Broadcasteronly, string shorthelptext = "usage: [%alias] ... Rights: %rights",Regex regex=null)
        {
            string[] list = new string[] { alias };
            cmdlist.Add(new BOTCOMMAND(method, flags, shorthelptext, regex,list));
        }

        // A much more general solution for extracting dymatic values into a text string. If we need to convert a text message to one containing local values, but the availability of those values varies by calling location
        // We thus build a table with only those values we have. 

        public class DynamicText
            {
            public List  <KeyValuePair<string,string>>  dynamicvariables=new List<KeyValuePair<string, string>>();  // A list of the variables available to us, we're using a list of pairs because the match we use uses BeginsWith,since the name of the string is unknown. The list is very short, so no biggie

            public bool AllowLinks=true;
            
            string Get(ref string fieldname) // Get the field. Failure is an option,  The fieldname may include extra characters. It is case sensitive.
            {
                string result = "";
                foreach (var entry in dynamicvariables)
                {
                    if (fieldname.StartsWith(entry.Key)) return entry.Value;
                }
                return result;
            }
 
            public DynamicText Add(string key, string value)
                {
                dynamicvariables.Add(new KeyValuePair<string, string>(key, value)); // Make the code slower but more readable :(
                return this;
                }

            public DynamicText()
                 {
                // BUG: These need be replaced if link generation is disabled.
                Add("endusage", "");

                if (AllowLinks)
                {
                    Add("beatsaver", "https://beatsaver.com");
                    Add("beatsaber", "https://beatsaber.com");
                    Add("scoresaber", "https://scoresaber.com");
                }
                else
                {
                    Add("beatsaver", "beatsaver site");
                    Add("beatsaver", "beatsaber site");
                    Add("scoresaber", "scoresaber site");
                }

                Add("time", "00:00:00"); // BUG: Placeholder text
                Add("date", "2019-01-01"); // BUG: Placeholder, insert code here
                }

            // To make this efficient, The return type needs to be a ref (using ref struct for the class). c# 7.2 supports this. This might be ugly IRL. Not sure if Unused return types execute a copy (assume not).
            public DynamicText AddUser(ref TwitchUser user)
                {
                Add("user", user.displayName);

                return this;
                }

            public DynamicText AddBotCmd(ref BOTCOMMAND botcmd)
            {

                StringBuilder aliastext = new StringBuilder();
                foreach (var alias in botcmd.aliases) aliastext.Append($"!{alias} ");
                Add("alias", aliastext.ToString());

                aliastext.Clear();
                aliastext.Append('[');
                aliastext.Append(botcmd.cmdflags & CmdFlags.TwitchLevel).ToString();
                aliastext.Append(']');
                Add("rights", aliastext.ToString());
                return this;
            }

            // Adds a JSON object to the dictionary. You can define a prefix to make the object identifiers unique if needed.
            public DynamicText AddJSON (ref JSONObject json, string prefix="")
                {
                foreach (var element in json) Add(prefix + element.Key, element.Value);
                return this;
                }

            public DynamicText AddSong(ref JSONObject json, string prefix = "")
                {
                AddJSON(ref json, prefix);
                Add("StarRating", GetStarRating(ref json));
                Add("Rating", GetRating(ref json));
                return this;
                }


            public string Parse(ref string text,bool parselong=false)
                {
                StringBuilder msgtext = new StringBuilder();
                string[] parts = text.Split(new char[] { '%' }); // Split entire help message by % boundaries

             
                if (parts.Length == 0) return "";
                for (int i = 0; i < parts.Length; i++)
                    {
 
                        bool found=false;
                        foreach (var entry in dynamicvariables)
                        {
                            if (parts[i].StartsWith(entry.Key))
                                {
                            if (entry.Key == "endusage" && !parselong) return msgtext.ToString(); // BUG: This works, but isn't the most elegant solution. Look into this later.
 
                                msgtext.Append (entry.Value);
                                msgtext.Append(parts[i].Substring(entry.Key.Length));
                                found = true;
                                break;
                                }
                        }
                    if (found) continue;

                    if (i!=0) msgtext.Append('%'); // Basically, we need to put the %'s back that were removed by split. The first % though is always fake.
                    msgtext.Append(parts[i]);
                    }

              return msgtext.ToString();
                }

            public DynamicText QueueMessage(string text, bool parselong = false)
            {
                QueueMessage(ref text, parselong);
                return this;
            }


            public DynamicText QueueMessage(ref string text,bool parselong=false)
                {
                Instance.QueueChatMessage(Parse(ref text,parselong));
                return this;
                }

            }


        public static void ParseHelpMessage(ref string message, ref BOTCOMMAND botcmd, ref TwitchUser user, ref string param, bool parselong = false)
                {

                // I will surely go to C sharp hell for this. (this may even work well in C# 7.2)

                new DynamicText().AddUser(ref user).AddBotCmd(ref botcmd).QueueMessage(ref message,parselong);

                }


        public static void ShowHelpMessage(ref BOTCOMMAND botcmd,ref TwitchUser user, string param,bool showlong) 
            {
            if (!botcmd.cmdflags.HasFlag(CmdFlags.UsageHelp)) return; // Make sure we're allowed to show help

            string helpmsg = botcmd.ShortHelp;


            ParseHelpMessage(ref helpmsg,ref  botcmd, ref user, ref param,showlong);
                            // Quick and dirty help text variable expander, this is a bit of a hack!
            
            return;
            }


        private void nop(TwitchUser requestor, string request)
            {
            // This is command does nothing, it can be used as a placeholder for help text aliases.
            }

        // Get help on a command
        private void help(TwitchUser requestor, string request)
            {
            if (request == "")
            {
                var msg = new QueueLongMessage();
                msg.Header("Usage: help < ");
                foreach (var entry in NewCommands)
                    {
                    var botcmd = entry.Value;
                    if (HasRights(ref botcmd,ref requestor))
                    msg.Add($"{entry.Key}", " ");
                    }
                msg.Add(">");
                msg.end("...", $"No commands available >");
                return;
            }
            if (NewCommands.ContainsKey(request.ToLower()))
                {
                var BotCmd = NewCommands[request.ToLower()];
                ShowHelpMessage(ref BotCmd, ref requestor, request, true);
                }            
            }

        public static bool HasRights(ref BOTCOMMAND botcmd,ref TwitchUser user)
        {
            if (botcmd.cmdflags.HasFlag(CmdFlags.Everyone)) return true; // Not sure if this is the best approach actually, not worth thinking about right now
            if (user.isBroadcaster & botcmd.cmdflags.HasFlag(CmdFlags.Broadcaster)) return true;
            if (user.isMod & botcmd.cmdflags.HasFlag(CmdFlags.Mod)) return true;
            if (user.isSub & botcmd.cmdflags.HasFlag(CmdFlags.Sub)) return true;
            if (user.isVip & botcmd.cmdflags.HasFlag(CmdFlags.VIP)) return true;
            return false;

        }

        public static void ExecuteCommand(string command, ref TwitchUser user, string param)
        {
        var botcmd= NewCommands[command];

            // Check permissions first

            bool allow = HasRights(ref botcmd,ref user);

            if (!allow)
                {
                CmdFlags twitchpermission = botcmd.cmdflags & CmdFlags.TwitchLevel;
                if (!botcmd.cmdflags.HasFlag(CmdFlags.SilentPreflight)) Instance?.QueueChatMessage($"{command} is restricted to {twitchpermission.ToString()}");
                return;
                }

            if (param == "?") // Handle per command help requests - If permitted.
                {
                ShowHelpMessage(ref botcmd, ref user, param,true);
                return;
                }

            // Check regex
            
            if (!botcmd.regexfilter.IsMatch(param))
                {
                ShowHelpMessage(ref botcmd, ref user, param, false);
                return;
                }


            try
            {
            botcmd.Method(user, param); // Call the command
            }
        catch (Exception ex)
            {
            // Display failure message, and lock out command for a time period. Not yet.

            Plugin.Log(ex.ToString());

            }

        }
        #endregion

        public static void Parse(TwitchUser user, string request)
        {
            if (!Instance) return;
            if (!request.StartsWith("!")) return;

            string[] parts = request.Split(new char[] { ' ' }, 2);

            if (parts.Length <= 0) return;

            string command = parts[0].Substring(1)?.ToLower();
            if (NewCommands.ContainsKey(command))
            {
                string param = parts.Length > 1 ? parts[1] : "";
                if (deck.ContainsKey(command))
                {
                    param = command;
                    if (parts.Length > 1) param += " " + parts[1];
                }
                //Commands[command]?.Invoke(user, param);
                ExecuteCommand(command,ref user,param);
            }
        }



    }
}



