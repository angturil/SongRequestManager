#if REQUEST_BOT

using CustomUI.BeatSaber;
using EnhancedTwitchChat.Chat;
using EnhancedTwitchChat.Textures;
using EnhancedTwitchChat.UI;
using EnhancedTwitchChat.Utils;
using HMUI;
using EnhancedTwitchChat.SimpleJSON;
//using SongBrowserPlugin;
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
using System.Timers;

#if OLDVERSION
using TMPro;
#endif

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using VRUI;
using Image = UnityEngine.UI.Image;
using Toggle = UnityEngine.UI.Toggle;
using TMPro;
using EnhancedTwitchChat.Config;

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


        public static RequestBot Instance;
        public static ConcurrentQueue<RequestInfo> UnverifiedRequestQueue = new ConcurrentQueue<RequestInfo>();
        public static ConcurrentQueue<KeyValuePair<SongRequest, bool>> BlacklistQueue = new ConcurrentQueue<KeyValuePair<SongRequest, bool>>();
        public static Dictionary<string, RequestUserTracker> RequestTracker = new Dictionary<string, RequestUserTracker>();

        private static Button _requestButton;
        private static bool _refreshQueue = false;

        private static Queue<string> _botMessageQueue = new Queue<string>();

        //private static Dictionary<string, BOTCOMMAND> NewCommands = new Dictionary<string, BOTCOMMAND>(); // BUG: Still not the final form

#if UNRELEASED
        //static private string CommandEveryXminutes ="!add waterbreak song";   // BUG: Not yet iplemented
#endif

        bool _mapperWhitelist = false; // BUG: Need to clean these up a bit.
        bool _configChanged = false;

        private static System.Random generator = new System.Random(); // BUG: Should at least seed from unity?

        public static List<JSONObject> played = new List<JSONObject>(); // Played list

        private static StringListManager mapperwhitelist = new StringListManager(); // BUG: This needs to switch to list manager interface
        private static StringListManager mapperBanlist = new StringListManager(); // BUG: This needs to switch to list manager interface

        private static string duplicatelist = "duplicate.list"; // BUG: Name of the list, needs to use a different interface for this.

        private static Dictionary<string, string> songremap = new Dictionary<string, string>();
        private static Dictionary<string, string> deck = new Dictionary<string, string>(); // deck name/content

        public static string datapath; // Location of all local data files

        private static CustomMenu _songRequestMenu = null;
        private static RequestBotListViewController _songRequestListViewController = null;

        public static string playedfilename = "";

        public static void OnLoad()
        {
            var _levelListViewController = Resources.FindObjectsOfTypeAll<LevelPackLevelsViewController>().First();
            if (_levelListViewController)
            {
                _requestButton = BeatSaberUI.CreateUIButton(_levelListViewController.rectTransform, "QuitButton", new Vector2(63, -3.5f),
                    new Vector2(15.0f, 5.5f), () => { _requestButton.interactable = false; _songRequestMenu.Present(); _requestButton.interactable = true; }, "Song Requests");

                (_requestButton.transform as RectTransform).anchorMin = new Vector2(1, 1);
                (_requestButton.transform as RectTransform).anchorMax = new Vector2(1, 1);

                _requestButton.ToggleWordWrapping(false);
                _requestButton.SetButtonTextSize(2.0f);
                BeatSaberUI.AddHintText(_requestButton.transform as RectTransform, $"{(!RequestBotConfig.Instance.RequestBotEnabled ? "To enable the song request bot, look in the Enhanced Twitch Chat settings menu." : "Manage the current request queue")}");

                UpdateRequestUI();
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

            WriteQueueSummaryToFile();
            WriteQueueStatusToFile(QueueMessage(RequestBotConfig.Instance.RequestQueueOpen));


            if (Instance) return;
            new GameObject("EnhancedTwitchChatRequestBot").AddComponent<RequestBot>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Instance = this;

            datapath = Path.Combine(Environment.CurrentDirectory, "UserData", "EnhancedTwitchChat");
            if (!Directory.Exists(datapath))
                Directory.CreateDirectory(datapath);

            playedfilename = Path.Combine(datapath, "played.json"); // Record of all the songs played in the current session

            string filesToDelete = Path.Combine(Environment.CurrentDirectory, "FilesToDelete");
            if (Directory.Exists(filesToDelete))
                Utilities.EmptyDirectory(filesToDelete);


            TimeSpan PlayedAge = GetFileAgeDifference(playedfilename);
            if (PlayedAge < TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours)) played = ReadJSON(playedfilename); // Read the songsplayed file if less than x hours have passed 

            RequestQueue.Read(); // Might added the timespan check for this too. To be decided later.
            RequestHistory.Read();
            SongBlacklist.Read();

            listcollection.ClearOldList("duplicate.list", TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours));

            UpdateRequestUI();
            InitializeCommands();

            COMMAND.CommandConfiguration();
            RunStartupScripts();

            StartCoroutine(ProcessRequestQueue());
            StartCoroutine(ProcessBlacklistRequests());

            RequestBotConfig.Instance.ConfigChangedEvent += OnConfigChangedEvent;
        }

        private void OnConfigChangedEvent(RequestBotConfig config)
        {
            _configChanged = true;
        }


        private void OnConfigChanged()
        {
            UpdateRequestUI();

            if (RequestBotListViewController.Instance.isActivated)
                RequestBotListViewController.Instance.UpdateRequestUI(true);

            _configChanged = false;
        }


        
        // BUG: Prototype code, used for testing.
        class BotEvent
        {
            public static List<BotEvent> events = new List<BotEvent>();

            public DateTime time;
            public string command;
            public bool repeat;
            Timer timeq;

        public static void Clear()
            {                
                foreach (var ev in events) ev.timeq.Stop();
            }
        public BotEvent(DateTime time,string command,bool repeat)
            {
                this.time = time;
                this.command = command;
                this.repeat = repeat;
                timeq = new System.Timers.Timer(1000);
                timeq.Elapsed += (s, args) => ScheduledCommand(command, args);
                timeq.AutoReset = true;
                timeq.Enabled = true;
            }

        public BotEvent(TimeSpan delta, string command, bool repeat=false)
            {
                this.command = command;
                this.repeat = repeat;
                timeq = new System.Timers.Timer(delta.TotalMilliseconds);
                timeq.Elapsed += (s, args) => ScheduledCommand(command, args);
                timeq.AutoReset = repeat;

                events.Add(this);

                timeq.Enabled = true;
            }
        }

 

        public static void ScheduledCommand(string command, System.Timers.ElapsedEventArgs e)
            {
            COMMAND.Parse(TwitchWebSocketClient.OurTwitchUser, command);
            }

        private void RunStartupScripts()
        {
            ReadRemapList(); // BUG: This should use list manager

#if UNRELEASED


            OpenList(TwitchWebSocketClient.OurTwitchUser, "mapper.list"); // Open mapper list so we can get new songs filtered by our favorite mappers.
            MapperAllowList(TwitchWebSocketClient.OurTwitchUser, "mapper.list");
            loaddecks(TwitchWebSocketClient.OurTwitchUser, ""); // Load our default deck collection
            // BUG: Command failure observed once, no permission to use /chatcommand. Possible cause: Ourtwitchuser isn't authenticated yet.

            RunScript(TwitchWebSocketClient.OurTwitchUser, "startup.script"); // Run startup script. This can include any bot commands.
#endif
        }

        private void FixedUpdate()
        {
            if (_configChanged)
                OnConfigChanged();

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

                        if (!silence) QueueChatMessage($"{request.Key.song["songName"].Value}/{request.Key.song["authorName"].Value} ({songId}) added to the blacklist.");
                    }
                }
            }
        }

        private void SendChatMessage(string message)
        {
            try
            {
                Plugin.Log($"Sending message: \"{message}\"");
                TwitchWebSocketClient.SendMessage($"PRIVMSG #{TwitchLoginConfig.Instance.TwitchChannelName} :{message}");
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
            _botMessageQueue.Enqueue(RequestBotConfig.Instance.BotPrefix+message);
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

        private List<JSONObject> GetSongListFromResults(JSONNode result, ref string errorMessage)
        {
            List<JSONObject> songs = new List<JSONObject>(); 
            if (result["songs"].IsArray)
            {
                // Might consider sorting the list by rating to improve quality of results            
                foreach (JSONObject currentSong in result["songs"].AsArray)
                {
                    errorMessage = SongSearchFilter(currentSong, false);
                    if (errorMessage == "")
                        songs.Add(currentSong);
                }
            }
            else
            {
                songs.Add(result["song"].AsObject);
            }
            return songs;
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

            JSONNode result = null;
            // Get song query results from beatsaver.com
            string requestUrl = requestInfo.isBeatSaverId ? $"https://beatsaver.com/api/songs/detail/{request}" : $"https://beatsaver.com/api/songs/search/song/{request}";
            yield return Utilities.Download(requestUrl, Utilities.DownloadType.Raw, null,
                // Download success
                (web) =>
                {
                    result = JSON.Parse(web.downloadHandler.text);
                },
                // Download failed,  song probably doesn't exist on beatsaver
                (web) =>
                {
                    QueueChatMessage($"Invalid BeatSaver ID \"{request}\" specified. {requestUrl}");
                }
            );
            if (result == null) yield break;

            // Make sure we actually found 1+ songs
            if (result["songs"].IsArray && result["total"].AsInt == 0)
            {
                QueueChatMessage($"No results found for request \"{request}\"");
                yield break;
            }
            yield return null;

            string errorMessage = "";
            List<JSONObject> songs = GetSongListFromResults(result, ref errorMessage);
            // Filter out too many or too few results
            if (songs.Count == 0)
            {
                if (errorMessage == "")
                    errorMessage = $"No results found for request \"{request}\"";
            }
            else if (!RequestBotConfig.Instance.AutopickFirstSong && songs.Count >= 4)
            {
                errorMessage = $"Request for '{request}' produces {songs.Count} results, narrow your search by adding a mapper name, or use https://beatsaver.com to look it up.";
            }
            else if (!RequestBotConfig.Instance.AutopickFirstSong && songs.Count > 1 && songs.Count < 4)
            {
                var msg = new QueueLongMessage(1, 5);
                msg.Header($"@{requestor.displayName}, please choose: ");
                foreach (var eachsong in songs) msg.Add(new DynamicText().AddSong(eachsong).Parse(BsrSongDetail), ", ");
                msg.end("...", $"No matching songs for for {request}");
                yield break;
            }
            else
            {
                if (isNotModerator(requestor) || !requestInfo.isBeatSaverId) errorMessage = SongSearchFilter(songs[0], false);
            }

            // Display reason why chosen song was rejected, if filter is triggered. Do not add filtered songs
            if (errorMessage != "")
            {
                QueueChatMessage(errorMessage);
                yield break;
            }
            
            var song = songs[0];

            RequestTracker[requestor.id].numRequests++;

            listcollection.add(duplicatelist, song["id"].Value);
            if ((requestInfo.flags.HasFlag(CmdFlags.MoveToTop)))
                RequestQueue.Songs.Insert(0, new SongRequest(song, requestor, requestInfo.requestTime, RequestStatus.Queued, requestInfo.requestInfo));
            else
                RequestQueue.Songs.Add(new SongRequest(song, requestor, requestInfo.requestTime, RequestStatus.Queued, requestInfo.requestInfo));

            RequestQueue.Write();

            Writedeck(requestor, "savedqueue"); // This can be used as a backup if persistent Queue is turned off.

            new DynamicText().AddSong(ref song).QueueMessage(AddSongToQueueText.ToString());

            UpdateRequestUI();
            _refreshQueue = true;
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

                    Plugin.Log("Downloading");

                    string localPath = Path.Combine(Environment.CurrentDirectory, ".requestcache", $"{songIndex}.zip");
                    yield return Utilities.DownloadFile(request.song["downloadUrl"].Value, localPath);
                    yield return Utilities.ExtractZip(localPath, currentSongDirectory);
                    yield return new WaitUntil(() => SongLoader.AreSongsLoaded && !SongLoader.AreSongsLoading);
                    yield return SongListUtils.RetrieveNewSong(songIndex, true);
                    
                    Utilities.EmptyDirectory(".requestcache", true);
                    levels = SongLoader.CustomLevels.Where(l => l.levelID.StartsWith(songHash)).ToArray();
                }
                else
                {
                    Plugin.Log($"Song {songName} already exists!");
                }

                if (!retried)
                {
                    // Dismiss the song request viewcontroller now
                    _songRequestMenu.Dismiss();
                }

                if (levels.Length > 0)
                {
                    Plugin.Log($"Scrolling to level {levels[0].levelID}");

                    bool success = false;
                    yield return SongListUtils.ScrollToLevel(levels[0].levelID, (s) => success = s, false);

                    // Redownload the song if we failed to scroll to it
                    if (!success && !retried)
                    {
                        retried = true;
                        goto retry;
                    }
                }
                else
                {
                    Plugin.Log("Failed to find new level!");
                }

                if (!request.song.IsNull) new DynamicText().AddSong(request.song).QueueMessage(NextSonglink.ToString()); // Display next song message
            }
        }
        
        private static void UpdateRequestUI(bool writeSummary = true)
        {
            try
            {
                if (writeSummary)
                    WriteQueueSummaryToFile(); // Write out queue status to file, do it first

                _requestButton.interactable = RequestBotConfig.Instance.RequestBotEnabled;

                if (RequestQueue.Songs.Count == 0 || !RequestBotConfig.Instance.RequestBotEnabled)
                {
                    _requestButton.gameObject.GetComponentInChildren<Image>().color = Color.red;
                }
                else
                {
                    _requestButton.gameObject.GetComponentInChildren<Image>().color = Color.green;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
        }

        public static void DequeueRequest(SongRequest request, bool updateUI = true)
        {
            RequestHistory.Songs.Insert(0, request);
            if (RequestHistory.Songs.Count > RequestBotConfig.Instance.RequestHistoryLimit)
            {
                int diff = RequestHistory.Songs.Count - RequestBotConfig.Instance.RequestHistoryLimit;
                RequestHistory.Songs.RemoveRange(RequestHistory.Songs.Count - diff - 1, diff);
            }
            RequestQueue.Songs.Remove(request);
            RequestHistory.Write();
            RequestQueue.Write();

            // Decrement the requestors request count, since their request is now out of the queue
            if (RequestTracker.ContainsKey(request.requestor.id)) RequestTracker[request.requestor.id].numRequests--;

            if (updateUI == false) return;

            UpdateRequestUI();
            _refreshQueue = true;
        }

        public static SongRequest DequeueRequest(int index, bool updateUI = true)
        {
            SongRequest request = RequestQueue.Songs.ElementAt(index);

            if (request != null)
                DequeueRequest(request, updateUI);

#if UNRELEASED
            // If the queue is empty, Execute a custom command, the could be a chat message, a deck request, or nothing
            try
            {
                if (RequestBotConfig.Instance.RequestQueueOpen && updateUI == true && RequestQueue.Songs.Count == 0) RequestBot.listcollection.runscript("emptyqueue.script");
            }
            catch (Exception ex) { Plugin.Log(ex.ToString()); }
#endif


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


        private void AddToTop(TwitchUser requestor, string request, CmdFlags flags = 0, string info = "")
        {
            ProcessSongRequest(requestor, request, CmdFlags.MoveToTop, "ATT");
        }

        private void ProcessSongRequest(TwitchUser requestor, string request, CmdFlags flags = 0, string info = "")
        {
            try
            {
                if (RequestBotConfig.Instance.RequestQueueOpen == false && isNotModerator(requestor)) // BUG: Complex permission, Queue state message needs to be handled higher up
                {
                    QueueChatMessage($"Queue is currently closed.");
                    return;
                }


                if (!RequestTracker.ContainsKey(requestor.id))
                    RequestTracker.Add(requestor.id, new RequestUserTracker());

                int limit = RequestBotConfig.Instance.UserRequestLimit;
                if (requestor.isSub) limit = Math.Max(limit, RequestBotConfig.Instance.SubRequestLimit);
                if (requestor.isMod) limit = Math.Max(limit, RequestBotConfig.Instance.ModRequestLimit);
                if (requestor.isVip) limit += RequestBotConfig.Instance.VipBonusRequests; // Current idea is to give VIP's a bonus over their base subscription class, you can set this to 0 if you like

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

                        new DynamicText().Add("Requests", RequestTracker[requestor.id].numRequests.ToString()).Add("RequestLimit", RequestBotConfig.Instance.SubRequestLimit.ToString()).QueueMessage("You already have %Requests% on the queue. You can add another once one is played. Subscribers are limited to %RequestLimit%.");

                        return;
                    }
                }

                RequestInfo newRequest = new RequestInfo(requestor, request, DateTime.UtcNow, _digitRegex.IsMatch(request) || _beatSaverRegex.IsMatch(request), flags, info);

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


    }
}

#endif