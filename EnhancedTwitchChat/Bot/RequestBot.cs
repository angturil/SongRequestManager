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

        [Flags]
        public enum CmdFlags
        {
            None = 0,
            Everyone = 1, // Im
            Sub = 2,
            Mod = 4,
            Broadcaster = 8,
            VIP = 16,
            UserList = 32,  // If this is enabled, users on a list are allowed to use a command (this is an OR, so leave restrictions to Broadcaster if you want ONLY users on a list)
            TwitchLevel = 63, // This is used to show ONLY the twitch user flags when showing permissions

            ShowRestrictions = 64, // Using the command without the right access level will show permissions error. Mostly used for commands that can be unlocked at different tiers.

            BypassRights = 128, // Bypass right check on command, allowing error messages, and a later code based check. Often used for help only commands. 
            QuietFail = 256, // Return no results on failed preflight checks.

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

            MoveToTop = 1 << 25, // Private, used by ATT command. Its possible to have multiple aliases for the same flag

            Disabled = 1 << 30, // If ON, the command will not be added to the alias list at all.
        }

        const CmdFlags Default = 0;
        const CmdFlags Everyone = Default | CmdFlags.Everyone;
        const CmdFlags Broadcasteronly = Default | CmdFlags.Broadcaster;
        const CmdFlags Mod = Default | CmdFlags.Broadcaster | CmdFlags.Mod;
        const CmdFlags Sub = Default | CmdFlags.Sub;
        const CmdFlags VIP = Default | CmdFlags.VIP;
        const CmdFlags Help = CmdFlags.BypassRights;


        #region common Regex expressions

        private static readonly Regex _digitRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _beatSaverRegex = new Regex("^[0-9]+-[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _alphaNumericRegex = new Regex("^[0-9A-Za-z]+$", RegexOptions.Compiled);
        private static readonly Regex _RemapRegex = new Regex("^[0-9]+,[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _beatsaversongversion = new Regex("^[0-9]+$|^[0-9]+-[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _nothing = new Regex("$^", RegexOptions.Compiled);
        private static readonly Regex _anything = new Regex(".*", RegexOptions.Compiled); // Is this the most efficient way?
        private static readonly Regex _atleast1 = new Regex("..*", RegexOptions.Compiled); // Allow usage message to kick in for blank 
        private static readonly Regex _fail = new Regex("(?!x)x", RegexOptions.Compiled); // Not sure what the official fastest way to auto-fail a match is, so this will do
        private static readonly Regex _deck = new Regex("^(current|draw|first|last|random|unload)$|$^", RegexOptions.Compiled); // Checks deck command parameters

        private static readonly Regex _drawcard = new Regex("($^)|(^[0-9]+$|^[0-9]+-[0-9]+$)", RegexOptions.Compiled);

        #endregion

        public static RequestBot Instance;
        public static ConcurrentQueue<RequestInfo> UnverifiedRequestQueue = new ConcurrentQueue<RequestInfo>();
        public static ConcurrentQueue<KeyValuePair<SongRequest, bool>> BlacklistQueue = new ConcurrentQueue<KeyValuePair<SongRequest, bool>>();
        public static Dictionary<string, RequestUserTracker> RequestTracker = new Dictionary<string, RequestUserTracker>();

        private static Button _requestButton;
        private static bool _refreshQueue = false;

        private static FlowCoordinator _levelSelectionFlowCoordinator;
        private static DismissableNavigationController _levelSelectionNavigationController;
        private static Queue<string> _botMessageQueue = new Queue<string>();

        //private static Dictionary<string, BOTCOMMAND> NewCommands = new Dictionary<string, BOTCOMMAND>(); // BUG: Still not the final form

#if UNRELEASED
        //static private string CommandEveryXminutes ="!add waterbreak song";   // BUG: Not yet iplemented
#endif

        bool mapperwhiteliston = false; // BUG: Need to clean these up a bit.

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
            datapath = Path.Combine(Environment.CurrentDirectory, "UserData", "EnhancedTwitchChat");
            if (!Directory.Exists(datapath))
                Directory.CreateDirectory(datapath);

            playedfilename = Path.Combine(datapath, "played.json"); // Record of all the songs played in the current session

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

            WriteQueueSummaryToFile();
            WriteQueueStatusToFile(QueueMessage(Config.Instance.QueueOpen));


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


            TimeSpan PlayedAge = GetFileAgeDifference(playedfilename);
            if (PlayedAge < TimeSpan.FromHours(Config.Instance.SessionResetAfterXHours)) played = ReadJSON(playedfilename); // Read the songsplayed file if less than x hours have passed 

            RequestQueue.Read(); // Might added the timespan check for this too. To be decided later.

            RequestHistory.Read();
            SongBlacklist.Read();

            listcollection.ClearOldList("duplicate.list", TimeSpan.FromHours(Config.Instance.SessionResetAfterXHours));

            UpdateRequestButton();
            InitializeCommands();

            RunStartupScripts();

            StartCoroutine(ProcessRequestQueue());
            StartCoroutine(ProcessBlacklistRequests());


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

            //QueueChatMessage($"request={requestInfo.requestInfo}");

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
                    var msg = new QueueLongMessage(1, 5);

                    msg.Header($"@{requestor.displayName}, please choose: ");
                    foreach (var eachsong in songs) msg.Add(new DynamicText().AddSong(eachsong).Parse(ref BsrSongDetail), ", ");
                    msg.end("...", $"No matching songs for for {request}");
                    yield break;

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

                listcollection.add(duplicatelist, song["id"].Value);
                if ((requestInfo.flags.HasFlag(CmdFlags.MoveToTop)))
                    RequestQueue.Songs.Insert(0,new SongRequest(song, requestor, requestInfo.requestTime, RequestStatus.Queued, requestInfo.requestInfo));
                else
                    RequestQueue.Songs.Add(new SongRequest(song, requestor, requestInfo.requestTime, RequestStatus.Queued,requestInfo.requestInfo));

                RequestQueue.Write();

                Writedeck(requestor, "savedqueue"); // This can be used as a backup if persistent Queue is turned off.

                new DynamicText().AddSong(ref song).QueueMessage(AddSongToQueueText);

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


                    try
                    {
                        Plugin.Log($"Scrolling to level {levels[0].levelID}");
                        if (!SongListUtils.ScrollToLevel(levels[0].levelID) && !retried)
                        {
                            retried = true;
                            goto retry;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Display failure message, and lock out command for a time period. Not yet.

                        Plugin.Log(ex.ToString());

                    }

                }
                else
                {
                    Plugin.Log("Failed to find new level!");
                }

                if (!request.song.IsNull) new DynamicText().AddSong(request.song).QueueMessage(NextSonglink); // Display next song message

                _songRequestMenu.Dismiss();
            }
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

        public static void DequeueRequest(SongRequest request, bool updateUI = true)
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

            if (updateUI == false) return;

            UpdateRequestButton();
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
                if (Config.Instance.QueueOpen && updateUI == true && RequestQueue.Songs.Count == 0) RequestBot.listcollection.runscript("emptyqueue.script");
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


        private void AddToTop(TwitchUser requestor, string request, CmdFlags flags = 0, string info = "")
            {
            ProcessSongRequest(requestor, request, CmdFlags.MoveToTop, "ATT"); 
            }

        private void ProcessSongRequest(TwitchUser requestor, string request, CmdFlags flags = 0, string info = "")
        {
            try
            {
                if (Config.Instance.QueueOpen == false && isNotModerator(requestor)) // BUG: Complex permission, Queue state message needs to be handled higher up
                {
                    QueueChatMessage($"Queue is currently closed.");
                    return;
                }


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

                        new DynamicText().Add("Requests", RequestTracker[requestor.id].numRequests.ToString()).Add("RequestLimit", Config.Instance.SubRequestLimit.ToString()).QueueMessage("You already have %Requests% on the queue. You can add another once one is played. Subscribers are limited to %RequestLimit%.");

                        return;
                    }
                }

                RequestInfo newRequest = new RequestInfo(requestor, request, DateTime.UtcNow, _digitRegex.IsMatch(request) || _beatSaverRegex.IsMatch(request), flags,info);

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

    }
}



